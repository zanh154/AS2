
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
namespace AssignmentPRN.Tests;
public sealed class GradingRetryRegressionTests
{
 private sealed class PartialGrader : IAnswerGrader {
  public Task<AnswerGrade> GradeAsync(GradingContext ctx,CancellationToken ct=default)=>
   Task.FromResult(ctx.Transcript=="first" ? new AnswerGrade([new CriterionMark(1,10,"new mark")],null,null,null,1) : AnswerGrade.None);
 }
 [Fact]
 public async Task Audit_partial_regrade_must_keep_total_consistent_with_saved_answer_marks() {
  var clock=new VoiceRoomTests.Clock();
  await using var db=await VoiceRoomTests.SeedAsync(clock);
  var questions=await db.ExamQuestions.OrderBy(x=>x.OrderNo).ToListAsync();
  (await db.ExamCandidates.SingleAsync()).Status=AssignmentPRN.DataAccess.Enums.CandidateStatus.Completed;
  db.Rubrics.AddRange(new Rubric {RubricId=1,QuestionId=1,TotalScore=10,Criteria=[new RubricCriteria {CriteriaId=1,CriteriaName="one",MaxScore=10,DisplayOrder=1}]},
    new Rubric {RubricId=2,QuestionId=2,TotalScore=10,Criteria=[new RubricCriteria {CriteriaId=2,CriteriaName="two",MaxScore=10,DisplayOrder=1}]});
  db.Answers.AddRange(new Answer {AnswerId=1,CandidateId=1,ExamQuestionId=questions[0].ExamQuestionId,Transcript="first"},new Answer {AnswerId=2,CandidateId=1,ExamQuestionId=questions[1].ExamQuestionId,Transcript="second"});
  db.AiEvaluations.AddRange(new AiEvaluation {AnswerId=1,AiScore=8,EvaluatedAt=DateTime.Now,Criteria=[new AiEvaluationCriteria {CriteriaId=1,Score=8}]},
    new AiEvaluation {AnswerId=2,AiScore=6,EvaluatedAt=DateTime.Now,Criteria=[new AiEvaluationCriteria {CriteriaId=2,Score=6}]});
  db.FinalResults.Add(new FinalResult {ExamId=1,CandidateId=1,AiScore=7,FinalScore=7});
  await db.SaveChangesAsync();db.ChangeTracker.Clear();
  var result=await new GradingService(new GradingRepository(db),new PartialGrader(),clock).GradeCandidateAsync(1);
  Assert.True(result.Success,result.Error);db.ChangeTracker.Clear();
  var scores=await db.AiEvaluations.OrderBy(x=>x.AnswerId).Select(x=>x.AiScore).ToListAsync();
  var total=(await db.FinalResults.SingleAsync()).AiScore;
  Console.WriteLine($"Saved answer marks={string.Join(",",scores)}; total={total}; expected mean={scores.Average()}");
  Assert.Equal(scores.Average(),total);
 }


 [Theory]
 [InlineData(false)][InlineData(true)]
 public async Task A_partial_retry_without_any_prior_mark_for_a_question_clears_the_stale_AI_total(bool lecturerOverride) {
  var clock=new VoiceRoomTests.Clock();await using var db=await VoiceRoomTests.SeedAsync(clock);
  var qs=await db.ExamQuestions.OrderBy(x=>x.OrderNo).ToListAsync();
  (await db.ExamCandidates.SingleAsync()).Status=AssignmentPRN.DataAccess.Enums.CandidateStatus.Completed;
  db.Rubrics.AddRange(new Rubric {RubricId=1,QuestionId=1,TotalScore=10,Criteria=[new RubricCriteria {CriteriaId=1,CriteriaName="one",MaxScore=10,DisplayOrder=1}]},
    new Rubric {RubricId=2,QuestionId=2,TotalScore=10,Criteria=[new RubricCriteria {CriteriaId=2,CriteriaName="two",MaxScore=10,DisplayOrder=1}]});
  db.Answers.AddRange(new Answer {AnswerId=1,CandidateId=1,ExamQuestionId=qs[0].ExamQuestionId,Transcript="first"},new Answer {AnswerId=2,CandidateId=1,ExamQuestionId=qs[1].ExamQuestionId,Transcript="second"});
  db.AiEvaluations.Add(new AiEvaluation {AnswerId=1,AiScore=8,EvaluatedAt=DateTime.Now,Criteria=[new AiEvaluationCriteria {CriteriaId=1,Score=8}]});
  db.FinalResults.Add(new FinalResult {ExamId=1,CandidateId=1,AiScore=8,LecturerScore=lecturerOverride?6:null,FinalScore=lecturerOverride?6:8});
  await db.SaveChangesAsync();db.ChangeTracker.Clear();
  var result=await new GradingService(new GradingRepository(db),new PartialGrader(),clock).GradeCandidateAsync(1);
  Assert.True(result.Success,result.Error);Assert.Null(result.Data!.SlotScore);db.ChangeTracker.Clear();
  var final=await db.FinalResults.SingleAsync();Assert.Null(final.AiScore);
  Assert.Equal(lecturerOverride?6m:(decimal?)null,final.FinalScore);
  Assert.Equal(10m,(await db.AiEvaluations.SingleAsync()).AiScore);
 }
}
