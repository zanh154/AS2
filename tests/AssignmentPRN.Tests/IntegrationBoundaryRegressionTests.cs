using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssignmentPRN.Tests;

public partial class DocumentOperationalTests
{
    [Fact]
    public async Task Audit_approved_RAG_question_must_enter_the_dealable_bank()
    {
        await using var db = await OpenAsync();
        var reviewed = await Service(db).ReviewQuestionAsync(1, 1,
            new(GeneratedQuestionStatus.Approved, "audit", "AI approved content", "Expected answer"));
        Assert.True(reviewed.Success, reviewed.Error);
        var pool = await new QuestionRepository(db, new ExamStatePolicy()).ListPoolIdsAsync(
            new AssignmentPRN.DataAccess.Contracts.QuestionPickRequest { CourseId = 1, Count = 1 });
        Console.WriteLine($"Approved generated_questions={await db.GeneratedQuestions.CountAsync(x => x.Status == GeneratedQuestionStatus.Approved)}; questions={await db.Questions.CountAsync()}; dealable pool={pool.Count}");
        Assert.NotEmpty(pool);
    }
}

public sealed class IntegrationBoundaryRegressionTests
{
    private static RubricSaveRequest Request(int questionId, int rubricId) => new() {
        QuestionId = questionId, RubricId = rubricId,
        Criteria = [new RubricCriteriaInput { CriteriaName = "Changed via audit", MaxScore = 10, DisplayOrder = 1 }]
    };

    [Fact]
    public async Task Audit_owned_question_must_not_authorize_foreign_rubric_id()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Users.Add(new User { UserId=2, RoleId=1, FullName="Other lecturer", Email="foreign@example.test", PasswordHash="unused", Status="Active" });
        (await db.Courses.FindAsync(2))!.LecturerId = 2;
        db.Questions.Add(new Question { QuestionId=2, CourseId=2, CreatedBy=2, QuestionText="Foreign question" });
        db.Rubrics.AddRange(
            new Rubric { RubricId=1, QuestionId=1, TotalScore=10, Criteria=[new RubricCriteria { CriteriaName="Own rubric",MaxScore=10,DisplayOrder=1 }] },
            new Rubric { RubricId=2, QuestionId=2, TotalScore=10, Criteria=[new RubricCriteria { CriteriaName="Foreign rubric",MaxScore=10,DisplayOrder=1 }] });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new RubricService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db)).SaveAsync(Request(1,2),1);
        db.ChangeTracker.Clear();
        var saved = await db.RubricCriteria.Where(x=>x.RubricId==2).SingleAsync();
        Console.WriteLine($"Service success={result.Success}; foreign criterion after forged RubricId: {saved.CriteriaName}");
        Assert.Equal("Foreign rubric",saved.CriteriaName);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Audit_editing_rubric_must_preserve_existing_AI_grading_breakdown()
    {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.Rubrics.Add(new Rubric { RubricId=1, QuestionId=1, TotalScore=10, Criteria=[new RubricCriteria { CriteriaId=1,CriteriaName="Original criterion",MaxScore=10,DisplayOrder=1 }] });
        var examQuestionId = await db.ExamQuestions.Select(x=>x.ExamQuestionId).SingleAsync();
        db.Answers.Add(new Answer { AnswerId=1, CandidateId=1, ExamQuestionId=examQuestionId, Transcript="Original spoken answer" });
        db.AiEvaluations.Add(new AiEvaluation { EvaluationId=1,AnswerId=1,AiScore=8,EvaluatedAt=DateTime.Now,Criteria=[new AiEvaluationCriteria { CriteriaId=1,Score=8,Comment="Original reason" }] });
        (await db.ExamCandidates.SingleAsync()).Status=AssignmentPRN.DataAccess.Enums.CandidateStatus.Completed;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result=await new RubricService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db)).SaveAsync(Request(1,1),1);
        Console.WriteLine($"Rubric edit success={result.Success}; AI evaluations={await db.AiEvaluations.CountAsync()}; criterion marks={await db.Set<AiEvaluationCriteria>().CountAsync()}");
        Assert.Equal(1,await db.Set<AiEvaluationCriteria>().CountAsync());
    }

    private sealed class PausedAnalyzer : IFollowUpAnalyzer
    {
        public TaskCompletionSource First { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Second { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public async Task<FollowUpDecision> AnalyzeAsync(FollowUpContext context,CancellationToken ct=default) {
            if(Interlocked.Increment(ref calls)==1) First.TrySetResult(); else Second.TrySetResult();
            await Release.Task;
            return new(FollowUpReason.MissingPoint,"Please explain the lock.");
        }
    }

    [Fact]
    public async Task Audit_refresh_during_AI_decision_must_not_start_hidden_next_question_timer()
    {
        var clock=new VoiceRoomTests.Clock();
        await using var db=await VoiceRoomTests.SeedAsync(clock);
        (await db.ExamSessions.SingleAsync()).MaxFollowUpCount=3;
        (await db.Set<AssignmentPRN.DataAccess.Entities.ExamVivaSettings>().SingleAsync()).MaxFollowUpsPerQuestion=1;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var analyzer=new PausedAnalyzer();
        var service=new VoiceRoomService(new VoiceRoomRepository(db,new ExamStatePolicy()),new QuestionService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db),null!),clock,analyzer:analyzer);
        var submit=service.SyncAsync(1,2,new VoiceAnswerRequest(1,"There is a lock.",true));
        await analyzer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.At=clock.At.AddSeconds(8);
        var refresh=service.SyncAsync(1,2);
        DateTime? hiddenAskedAt;
        try {
            await analyzer.Second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            hiddenAskedAt=await db.ExamQuestions.Where(x=>x.OrderNo==2).Select(x=>x.AskedAt).SingleAsync();
        } finally { analyzer.Release.TrySetResult(); }
        var responses=await Task.WhenAll(submit,refresh);
        Assert.All(responses,x=>Assert.True(x.Success,x.Error));
        Console.WriteLine($"Hidden next question AskedAt during analysis: {hiddenAskedAt}; visible question is probe={responses.Any(x=>x.Data?.Question?.FollowUpId!=null)}");
        Assert.Null(hiddenAskedAt);
    }

    [Fact]
    public async Task A_late_analysis_cannot_reopen_previous_turn_after_next_question_started()
    {
        var clock=new VoiceRoomTests.Clock();
        await using var db=await VoiceRoomTests.SeedAsync(clock);
        (await db.ExamSessions.SingleAsync()).MaxFollowUpCount=3;
        (await db.Set<AssignmentPRN.DataAccess.Entities.ExamVivaSettings>().SingleAsync()).MaxFollowUpsPerQuestion=1;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var analyzer=new PausedAnalyzer();
        VoiceRoomService Service(IFollowUpAnalyzer? model)=>new(new VoiceRoomRepository(db,new ExamStatePolicy()),
            new QuestionService(new QuestionRepository(db,new ExamStatePolicy()),new CatalogRepository(db),null!),clock,analyzer:model);
        var pending=Service(analyzer).SyncAsync(1,2,new VoiceAnswerRequest(1,"Initial answer",true));
        await analyzer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.At=clock.At.AddSeconds(8);
        try {
            var next=await Service(null).SyncAsync(1,2);
            Assert.True(next.Success,next.Error);Assert.Equal(2,next.Data!.Question!.OrderNo);
        } finally {analyzer.Release.TrySetResult();}
        var late=await pending;
        Assert.True(late.Success,late.Error);Assert.Equal(2,late.Data!.Question!.OrderNo);
        Assert.Empty(await db.FollowUpQuestions.ToListAsync());
    }
}