
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AssignmentPRN.Tests;
public sealed class RubricHistoryRegressionTests
{
 private static async Task<AivesDbContext> Seed() {
  var db=await IssuedPaperEditTests.OpenAsync();
  db.Rubrics.Add(new Rubric {RubricId=1,QuestionId=1,TotalScore=10,Criteria=[new RubricCriteria {CriteriaId=1,CriteriaName="Original criterion",MaxScore=10,DisplayOrder=1}]});
  db.Answers.Add(new Answer {AnswerId=1,CandidateId=1,ExamQuestionId=await db.ExamQuestions.Select(x=>x.ExamQuestionId).SingleAsync(),Transcript="Spoken answer"});
  db.AiEvaluations.Add(new AiEvaluation {EvaluationId=1,AnswerId=1,AiScore=8,EvaluatedAt=DateTime.Now,Criteria=[new AiEvaluationCriteria {CriteriaId=1,Score=8,Comment="Historical mark"}]});
  (await db.ExamCandidates.SingleAsync()).Status=AssignmentPRN.DataAccess.Enums.CandidateStatus.Completed;
  await db.SaveChangesAsync();db.ChangeTracker.Clear();return db;
 }
 [Fact]
 public async Task Audit_deleting_graded_rubric_must_not_delete_historical_criterion_marks() {
  await using var db=await Seed();
  var service=new RubricService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db));
  var result=await service.DeleteAsync(1,1);
  db.ChangeTracker.Clear();
  Console.WriteLine($"Delete accepted={result.Success}; retained historical marks={await db.AiEvaluationCriteria.CountAsync()}");
  Assert.Equal(1,await db.AiEvaluationCriteria.CountAsync());
 }
 [Fact]
 public async Task Audit_updating_existing_criterion_must_preserve_historical_max_score() {
  await using var db=await Seed();
  var service=new RubricService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db));
  var result=await service.SaveAsync(new RubricSaveRequest {QuestionId=1,RubricId=1,Criteria=[new RubricCriteriaInput {CriteriaId=1,CriteriaName="New criterion",MaxScore=2,DisplayOrder=1}]},1);
  db.ChangeTracker.Clear();
  var review=await new GradingRepository(db).LoadReviewAsync(1);
  var criterion=Assert.Single(Assert.Single(review!.Answers).Criteria);
  Console.WriteLine($"Edit accepted={result.Success}; historical awarded={criterion.Awarded}, historical maximum={criterion.MaxScore}");
  Assert.Equal(10m,criterion.MaxScore);
 }
}
