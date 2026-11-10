using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;

namespace AssignmentPRN.Tests;

/// <summary>
/// The microphone check happens before the first question opens. Time spent testing the
/// microphone must never come out of the time the student has to answer.
/// </summary>
public sealed class VoiceReadyGateTests
{
    private static async Task<AivesDbContext> EnteredButNotReadyAsync(VoiceRoomTests.Clock clock)
    {
        var db = await VoiceRoomTests.SeedAsync(clock, ready: false);
        return db;
    }

    [Fact]
    public async Task Entering_opens_no_question_and_starts_no_clock()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock, entered: false);

        var first = await VoiceRoomTests.Service(db, clock).EnterAsync(1, 2);
        var second = await VoiceRoomTests.Service(db, clock).EnterAsync(1, 2);

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.True(first.Data!.AwaitingReady);
        Assert.Null(first.Data.Question);
        db.ChangeTracker.Clear();
        Assert.All(await db.ExamQuestions.ToListAsync(), q => Assert.Null(q.AskedAt));
        // Voice mode is still recorded once, and only once.
        Assert.Single(await db.Set<ExamLog>().Where(x => x.Action == VoiceRoomRepository.StartedAction).ToListAsync());
    }

    [Fact]
    public async Task Time_spent_on_the_microphone_check_is_not_taken_from_the_first_question()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await EnteredButNotReadyAsync(clock);
        var waiting = await VoiceRoomTests.Service(db, clock).SyncAsync(1, 2);
        Assert.True(waiting.Data!.AwaitingReady);

        // Forty seconds on the microphone check, on a 30 second question.
        clock.At = clock.At.AddSeconds(40);
        db.ChangeTracker.Clear();
        var ready = await VoiceRoomTests.Service(db, clock).ReadyAsync(1, 2);

        Assert.True(ready.Success, ready.Error);
        Assert.False(ready.Data!.AwaitingReady);
        var question = ready.Data.Question!;
        Assert.Equal(1, question.OrderNo);
        Assert.Equal(TimeSpan.FromSeconds(30), question.Deadline - ready.Data.ServerNow);
        db.ChangeTracker.Clear();
        Assert.Single(await db.Set<ExamLog>().Where(x => x.Action == VoiceRoomRepository.ReadyAction).ToListAsync());
    }

    [Fact]
    public async Task Nothing_can_be_answered_or_recorded_before_the_check_is_done()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await EnteredButNotReadyAsync(clock);
        var service = VoiceRoomTests.Service(db, clock);

        var answer = await service.SyncAsync(1, 2, new VoiceAnswerRequest(1, "Trả lời trước.", Submit: true));
        db.ChangeTracker.Clear();
        var upload = await service.UploadAsync(1, 2, 1, "audio/webm", 12, 1, Stream.Null);

        Assert.False(answer.Success);
        Assert.False(upload.Success);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Answers.ToListAsync());
        Assert.All(await db.ExamQuestions.ToListAsync(), q => Assert.Null(q.AskedAt));
    }

    [Fact]
    public async Task Asking_again_does_not_restart_the_question()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await EnteredButNotReadyAsync(clock);
        var first = await VoiceRoomTests.Service(db, clock).ReadyAsync(1, 2);

        // A double click, a second tab, or a retest of the microphone mid-question.
        clock.At = clock.At.AddSeconds(12);
        db.ChangeTracker.Clear();
        var again = await VoiceRoomTests.Service(db, clock).ReadyAsync(1, 2);

        Assert.True(again.Success, again.Error);
        Assert.Equal(first.Data!.Question!.Deadline, again.Data!.Question!.Deadline);
        db.ChangeTracker.Clear();
        Assert.Single(await db.Set<ExamLog>().Where(x => x.Action == VoiceRoomRepository.ReadyAction).ToListAsync());
    }

    [Fact]
    public async Task A_room_already_under_way_before_the_update_is_not_put_on_hold()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await EnteredButNotReadyAsync(clock);
        // Opened by the previous version: the first question already has its clock.
        var question = await db.ExamQuestions.SingleAsync(x => x.OrderNo == 1);
        question.AskedAt = clock.At.LocalDateTime;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        clock.At = clock.At.AddSeconds(5);
        var state = await VoiceRoomTests.Service(db, clock).SyncAsync(1, 2);

        Assert.True(state.Success, state.Error);
        Assert.False(state.Data!.AwaitingReady);
        Assert.Equal(1, state.Data.Question!.OrderNo);
    }

    [Fact]
    public async Task A_slot_that_runs_out_during_the_check_still_closes()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await EnteredButNotReadyAsync(clock);

        clock.At = clock.At.AddHours(1);
        var state = await VoiceRoomTests.Service(db, clock).SyncAsync(1, 2);

        Assert.True(state.Success, state.Error);
        Assert.True(state.Data!.Completed);
        Assert.False(state.Data.AwaitingReady);
        db.ChangeTracker.Clear();
        Assert.Equal(CandidateStatus.Completed, (await db.ExamCandidates.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_room_that_was_never_entered_cannot_be_made_ready()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await VoiceRoomTests.SeedAsync(clock, entered: false);

        var ready = await VoiceRoomTests.Service(db, clock).ReadyAsync(1, 2);

        Assert.False(ready.Success);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Set<ExamLog>().ToListAsync());
    }
}
