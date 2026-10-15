using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;
using ExamSessionStatus = AssignmentPRN.DataAccess.Enums.ExamSessionStatus;

namespace AssignmentPRN.Tests;

public sealed class VoiceRoomTests
{
    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
        public override DateTimeOffset GetUtcNow() => At.ToUniversalTime();
    }

    internal static VoiceRoomService Service(AivesDbContext db, Clock clock) => new(
        new VoiceRoomRepository(db, new ExamStatePolicy()),
        new QuestionService(new QuestionRepository(db, new ExamStatePolicy()), new CatalogRepository(db), null!), clock);

    internal static async Task<AivesDbContext> SeedAsync(Clock clock, bool entered = true)
    {
        var db = await IssuedPaperEditTests.OpenAsync();
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.Add(new User { UserId = 2, RoleId = 3, FullName = "Student", Email = "student@example.test", PasswordHash = "x", Status = "Active" });
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.StudentId = 2;
        candidate.ScheduledTime = clock.At.LocalDateTime.AddMinutes(-1);
        candidate.Status = entered ? CandidateStatus.InProgress : CandidateStatus.Waiting;
        candidate.StartedAt = entered ? clock.At.LocalDateTime : null;
        var exam = await db.ExamSessions.SingleAsync();
        exam.StartTime = candidate.ScheduledTime.Value;
        exam.EndTime = exam.StartTime.AddMinutes(10);
        db.Set<AssignmentPRN.DataAccess.Entities.ExamVivaSettings>().Add(new() { ExamId = 1, SecondsPerQuestion = 30 });
        db.Questions.Add(new Question { QuestionId = 2, CourseId = 1, CreatedBy = 1, QuestionText = "Second question" });
        db.ExamQuestions.Add(new ExamQuestion { ExamId = 1, CandidateId = 1, QuestionId = 2, OrderNo = 2 });
        if (entered) db.Set<ExamLog>().Add(new() { ExamId = 1, CandidateId = 1, UserId = 2,
            Action = VoiceRoomRepository.StartedAction, Timestamp = clock.At.LocalDateTime });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task Entry_uses_existing_dealing_and_records_voice_mode_once()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock, false);
        var first = await Service(db, clock).EnterAsync(1, 2);
        Assert.True(first.Success, first.Error);
        var second = await Service(db, clock).EnterAsync(1, 2);
        Assert.True(second.Success, second.Error);
        Assert.Equal(first.Data!.Question!.Deadline, second.Data!.Question!.Deadline);
        Assert.Single(await db.Set<ExamLog>().ToListAsync());
    }

    [Fact]
    public async Task Reload_keeps_deadline_and_only_returns_current_question()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        var first = await Service(db, clock).SyncAsync(1, 2);
        Assert.True(first.Success, first.Error);
        clock.At = clock.At.AddSeconds(10);
        db.ChangeTracker.Clear();
        var reloaded = (await Service(db, clock).SyncAsync(1, 2)).Data!;
        Assert.Equal(first.Data!.Question!.Deadline, reloaded.Question!.Deadline);
        Assert.Equal(1, reloaded.Question.OrderNo);
        Assert.Null(await db.ExamQuestions.Where(x => x.OrderNo == 2).Select(x => x.AskedAt).SingleAsync());
    }

    [Fact]
    public async Task Draft_submit_retry_and_timeout_are_idempotent()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        var service = Service(db, clock);
        var state = (await service.SyncAsync(1, 2)).Data!;
        var id = state.Question!.ExamQuestionId;
        Assert.True((await service.SyncAsync(1, 2, new(id, "draft"))).Success);
        Assert.Null((await db.Answers.SingleAsync()).FinishedAt);
        var submitted = (await service.SyncAsync(1, 2, new(id, "final", true))).Data!;
        Assert.Equal(2, submitted.Question!.OrderNo);
        var retry = (await service.SyncAsync(1, 2, new(id, "tampered", true))).Data!;
        Assert.Equal(submitted.Question, retry.Question);
        Assert.Equal("final", (await db.Answers.SingleAsync()).Transcript);
        clock.At = submitted.Question.Deadline.AddSeconds(1);
        var finished = (await service.SyncAsync(1, 2)).Data!;
        Assert.True(finished.Completed);
        Assert.Null(finished.Question);
        Assert.Equal(2, await db.Answers.CountAsync());
        Assert.Equal(CandidateStatus.Completed, (await db.ExamCandidates.SingleAsync()).Status);
        Assert.Equal(ExamSessionStatus.Completed, (await db.ExamSessions.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("cancelled")]
    [InlineData("inactive")]
    [InlineData("future-question")]
    [InlineData("foreign-question")]
    [InlineData("long-transcript")]
    public async Task Invalid_access_or_payload_cannot_save_answers(string scenario)
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        if (scenario == "cancelled") (await db.ExamSessions.SingleAsync()).Status = ExamSessionStatus.Cancelled;
        if (scenario == "inactive") (await db.Users.SingleAsync(x => x.UserId == 2)).Status = "Locked";
        await db.SaveChangesAsync();
        var ids = await db.ExamQuestions.OrderBy(x => x.OrderNo).Select(x => x.ExamQuestionId).ToArrayAsync();
        var id = scenario == "foreign-question" ? 999 : scenario == "future-question" ? ids[1] : ids[0];
        var result = await Service(db, clock).SyncAsync(1, scenario == "owner" ? 1 : 2,
            new(id, scenario == "long-transcript" ? new string('x', 16001) : "answer"));
        Assert.False(result.Success);
        Assert.Empty(await db.Answers.ToListAsync());
    }

    [Fact]
    public async Task Whole_slot_expiry_closes_remaining_questions_without_extra_time()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        var first = (await Service(db, clock).SyncAsync(1, 2)).Data!;
        clock.At = first.SlotEnd.AddSeconds(10);
        var final = (await Service(db, clock).SyncAsync(1, 2)).Data!;
        Assert.True(final.Completed);
        Assert.All(await db.Answers.ToListAsync(), x => Assert.True(x.FinishedAt <= first.SlotEnd.LocalDateTime));
    }

    [Fact]
    public async Task Multiple_choice_endpoints_cannot_overwrite_voice_answers()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        var legacy = new QuestionService(new QuestionRepository(db, new ExamStatePolicy()), new CatalogRepository(db), null!);
        Assert.False((await legacy.SaveDraftAsync(1, 2, new Dictionary<int, int?>())).Success);
        Assert.False((await legacy.SubmitExamAsync(1, 2, new Dictionary<int, int?>())).Success);
        Assert.Empty(await db.Answers.ToListAsync());
    }

    [Fact]
    public async Task Rejected_payload_cannot_leak_partial_question_changes_into_a_later_save()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        Assert.False((await Service(db, clock).SyncAsync(1, 2, new(999, "invalid"))).Success);
        await db.SaveChangesAsync();
        Assert.All(await db.ExamQuestions.ToListAsync(), q => Assert.Null(q.AskedAt));
        Assert.Empty(await db.Answers.ToListAsync());
    }

    [Fact]
    public async Task Question_deadline_is_clamped_to_the_session_end()
    {
        var clock = new Clock();
        await using var db = await SeedAsync(clock);
        var end = clock.At.LocalDateTime.AddSeconds(5);
        (await db.ExamSessions.SingleAsync()).EndTime = end;
        await db.SaveChangesAsync();
        var state = (await Service(db, clock).SyncAsync(1, 2)).Data!;
        Assert.Equal(end, state.Question!.Deadline.LocalDateTime);
        clock.At = clock.At.AddSeconds(6);
        Assert.True((await Service(db, clock).SyncAsync(1, 2)).Data!.Completed);
    }
}
