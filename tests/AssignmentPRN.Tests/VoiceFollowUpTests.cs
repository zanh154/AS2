using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;

namespace AssignmentPRN.Tests;

/// <summary>
/// The AI examiner inside a real room, on a real database, with the model stubbed out.
/// What matters here is that a probe never costs the student time they were owed and
/// never leaves the room stuck: the model is the one part allowed to fail.
/// </summary>
public sealed class VoiceFollowUpTests
{
    /// <summary>Stands in for the model. Records what it was shown.</summary>
    private sealed class StubAnalyzer(FollowUpDecision decision) : IFollowUpAnalyzer
    {
        private readonly List<FollowUpContext> _seen = [];

        public IReadOnlyList<FollowUpContext> Seen => _seen;
        public int Calls => _seen.Count;
        public FollowUpDecision Decision { get; set; } = decision;

        public Task<FollowUpDecision> AnalyzeAsync(FollowUpContext context, CancellationToken cancellationToken = default)
        {
            _seen.Add(context);
            return Task.FromResult(Decision);
        }
    }

    /// <summary>A model that is down. The room has to carry on regardless.</summary>
    private sealed class ThrowingAnalyzer : IFollowUpAnalyzer
    {
        public Task<FollowUpDecision> AnalyzeAsync(FollowUpContext context, CancellationToken cancellationToken = default) =>
            Task.FromException<FollowUpDecision>(new HttpRequestException("model is down"));
    }

    private static readonly FollowUpDecision Probe =
        new(FollowUpReason.MissingPoint, "Bạn giải thích rõ hơn phần khoá được không?");

    private static VoiceRoomService Service(AivesDbContext db, VoiceRoomTests.Clock clock, IFollowUpAnalyzer? analyzer) =>
        new(new VoiceRoomRepository(db, new ExamStatePolicy()),
            new QuestionService(new QuestionRepository(db, new ExamStatePolicy()), new CatalogRepository(db), null!),
            clock, null, null, analyzer);

    /// <summary>Seeds a room whose session allows probes.</summary>
    private static async Task<AivesDbContext> SeedAsync(
        VoiceRoomTests.Clock clock, int perQuestion = 1, int perSlot = 3)
    {
        var db = await VoiceRoomTests.SeedAsync(clock);
        var settings = await db.Set<AssignmentPRN.DataAccess.Entities.ExamVivaSettings>().SingleAsync();
        settings.MaxFollowUpsPerQuestion = perQuestion;
        var exam = await db.ExamSessions.SingleAsync();
        exam.MaxFollowUpCount = perSlot;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static VoiceAnswerRequest Submit(int examQuestionId, string transcript) =>
        new(examQuestionId, transcript, Submit: true);

    [Fact]
    public async Task A_handed_in_answer_is_probed_and_the_probe_becomes_the_open_question()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var analyzer = new StubAnalyzer(Probe);

        var state = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Em nghĩ là có khoá."));

        Assert.True(state.Success, state.Error);
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(Probe.Question, state.Data!.Question!.Text);
        Assert.NotNull(state.Data.Question.FollowUpId);
        Assert.Equal(FollowUpReason.MissingPoint, state.Data.Question.FollowUpReason);
        // The probe hangs off the answer, and the question it probes stays handed in.
        var stored = await db.FollowUpQuestions.SingleAsync();
        Assert.Equal("MissingPoint", stored.Reason);
        Assert.Equal(1, stored.OrderNo);
        Assert.NotNull(stored.AskedAt);
        Assert.Null(stored.AnsweredAt);
    }

    [Fact]
    public async Task The_next_question_does_not_start_its_clock_while_a_probe_is_open()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);

        var probed = await Service(db, clock, new StubAnalyzer(Probe)).SyncAsync(1, 2, Submit(1, "Có khoá."));
        Assert.True(probed.Success, probed.Error);

        // Second question is still unasked: its deadline must be measured from when the
        // student actually sees it, not from when they handed in the first answer.
        db.ChangeTracker.Clear();
        var second = await db.ExamQuestions.SingleAsync(x => x.OrderNo == 2);
        Assert.Null(second.AskedAt);
    }

    [Fact]
    public async Task Answering_a_probe_closes_it_and_opens_the_next_question()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 1);
        var analyzer = new StubAnalyzer(Probe);

        var probed = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Có khoá."));
        var followUpId = probed.Data!.Question!.FollowUpId!.Value;

        clock.At = clock.At.AddSeconds(5);
        db.ChangeTracker.Clear();
        analyzer.Decision = FollowUpDecision.None;
        var next = await Service(db, clock, analyzer).SyncAsync(1, 2,
            new VoiceAnswerRequest(1, "Khoá bi quan trước khi đọc.", Submit: true, FollowUpId: followUpId));

        Assert.True(next.Success, next.Error);
        Assert.Null(next.Data!.Question!.FollowUpId);
        Assert.Equal(2, next.Data.Question.OrderNo);

        db.ChangeTracker.Clear();
        var stored = await db.FollowUpQuestions.SingleAsync();
        Assert.Equal("Khoá bi quan trước khi đọc.", stored.AnswerTranscript);
        Assert.NotNull(stored.AnsweredAt);
    }

    [Fact]
    public async Task The_analyzer_is_shown_the_transcript_and_the_question_but_builds_on_the_last_reply()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 2);
        var analyzer = new StubAnalyzer(Probe);

        await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Câu trả lời đầu."));
        Assert.Equal("Câu trả lời đầu.", analyzer.Seen[0].Transcript);
        Assert.Empty(analyzer.Seen[0].PreviousProbes);

        var followUpId = (await db.FollowUpQuestions.SingleAsync()).FollowUpId;
        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();
        await Service(db, clock, analyzer).SyncAsync(1, 2,
            new VoiceAnswerRequest(1, "Trả lời câu hỏi thêm.", Submit: true, FollowUpId: followUpId));

        // A second probe reads the reply to the first, and is told not to repeat it.
        Assert.Equal(2, analyzer.Calls);
        Assert.Equal("Trả lời câu hỏi thêm.", analyzer.Seen[1].Transcript);
        Assert.Equal([Probe.Question!], analyzer.Seen[1].PreviousProbes);
    }

    [Fact]
    public async Task The_per_question_budget_stops_the_probing()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 1);
        var analyzer = new StubAnalyzer(Probe);

        var probed = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Có khoá."));
        var followUpId = probed.Data!.Question!.FollowUpId!.Value;

        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();
        var next = await Service(db, clock, analyzer).SyncAsync(1, 2,
            new VoiceAnswerRequest(1, "Trả lời thêm.", Submit: true, FollowUpId: followUpId));

        // One probe per question was configured, so the second question opens instead.
        Assert.Equal(1, analyzer.Calls);
        Assert.Equal(2, next.Data!.Question!.OrderNo);
        Assert.Single(await db.FollowUpQuestions.ToListAsync());
    }

    [Fact]
    public async Task A_session_configured_for_no_probes_never_calls_the_model()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 0);
        var analyzer = new StubAnalyzer(Probe);

        var state = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Có khoá."));

        Assert.Equal(0, analyzer.Calls);
        Assert.Equal(2, state.Data!.Question!.OrderNo);
    }

    [Fact]
    public async Task An_answer_the_model_finds_sufficient_is_not_probed()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var analyzer = new StubAnalyzer(FollowUpDecision.None);

        var state = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Đầy đủ."));

        Assert.Equal(1, analyzer.Calls);
        Assert.Empty(await db.FollowUpQuestions.ToListAsync());
        Assert.Equal(2, state.Data!.Question!.OrderNo);
    }

    [Fact]
    public async Task A_model_that_is_down_does_not_stop_the_exam()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);

        var state = await Service(db, clock, new ThrowingAnalyzer()).SyncAsync(1, 2, Submit(1, "Có khoá."));

        Assert.True(state.Success, state.Error);
        Assert.Empty(await db.FollowUpQuestions.ToListAsync());
        // The room reports the turn as awaiting a decision that never came; the next
        // sync past the grace window opens the following question.
        Assert.True(state.Data!.AwaitingFollowUp || state.Data.Question?.OrderNo == 2);
    }

    [Fact]
    public async Task A_decision_that_never_arrives_releases_the_next_question()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);

        await Service(db, clock, new ThrowingAnalyzer()).SyncAsync(1, 2, Submit(1, "Có khoá."));

        clock.At = clock.At.Add(VoiceRoomService.AnalysisGrace).AddSeconds(1);
        db.ChangeTracker.Clear();
        var state = await Service(db, clock, new ThrowingAnalyzer()).SyncAsync(1, 2);

        Assert.True(state.Success, state.Error);
        Assert.False(state.Data!.AwaitingFollowUp);
        Assert.Equal(2, state.Data.Question!.OrderNo);
    }

    [Fact]
    public async Task A_probe_that_is_not_the_open_one_is_refused()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var probed = await Service(db, clock, new StubAnalyzer(Probe)).SyncAsync(1, 2, Submit(1, "Có khoá."));
        var followUpId = probed.Data!.Question!.FollowUpId!.Value;

        db.ChangeTracker.Clear();
        var state = await Service(db, clock, new StubAnalyzer(Probe)).SyncAsync(1, 2,
            new VoiceAnswerRequest(1, "Gửi nhầm câu.", Submit: true, FollowUpId: followUpId + 99));

        Assert.False(state.Success);
        db.ChangeTracker.Clear();
        Assert.Null((await db.FollowUpQuestions.SingleAsync()).AnsweredAt);
    }

    [Fact]
    public async Task A_probe_left_unanswered_closes_at_its_deadline()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var probed = await Service(db, clock, new StubAnalyzer(Probe)).SyncAsync(1, 2, Submit(1, "Có khoá."));
        var deadline = probed.Data!.Question!.Deadline;

        clock.At = deadline.AddSeconds(1);
        db.ChangeTracker.Clear();
        var state = await Service(db, clock, new StubAnalyzer(FollowUpDecision.None)).SyncAsync(1, 2);

        Assert.True(state.Success, state.Error);
        db.ChangeTracker.Clear();
        var stored = await db.FollowUpQuestions.SingleAsync();
        Assert.NotNull(stored.AnsweredAt);
        Assert.Null(stored.AnswerTranscript);
    }

    [Fact]
    public async Task A_probe_never_runs_past_the_question_it_probes()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var started = await Service(db, clock, null).SyncAsync(1, 2);
        var questionDeadline = started.Data!.Question!.Deadline;

        clock.At = clock.At.AddSeconds(5);
        db.ChangeTracker.Clear();
        var probed = await Service(db, clock, new StubAnalyzer(Probe)).SyncAsync(1, 2, Submit(1, "Có khoá."));

        Assert.Equal(questionDeadline, probed.Data!.Question!.Deadline);
    }

    [Fact]
    public async Task The_slot_is_not_finished_while_a_probe_on_the_last_answer_is_open()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 1, perSlot: 1);
        var analyzer = new StubAnalyzer(FollowUpDecision.None);

        await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Câu một."));
        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();

        analyzer.Decision = Probe;
        var state = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(2, "Câu hai."));

        Assert.False(state.Data!.Completed);
        Assert.NotNull(state.Data.Question!.FollowUpId);
        db.ChangeTracker.Clear();
        Assert.Equal(CandidateStatus.InProgress, (await db.ExamCandidates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Answering_the_last_probe_finishes_the_slot()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock, perQuestion: 1, perSlot: 1);
        var analyzer = new StubAnalyzer(FollowUpDecision.None);

        await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Câu một."));
        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();
        analyzer.Decision = Probe;
        var probed = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(2, "Câu hai."));
        var followUpId = probed.Data!.Question!.FollowUpId!.Value;

        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();
        var state = await Service(db, clock, analyzer).SyncAsync(1, 2,
            new VoiceAnswerRequest(2, "Nói thêm.", Submit: true, FollowUpId: followUpId));

        Assert.True(state.Data!.Completed);
        db.ChangeTracker.Clear();
        Assert.Equal(CandidateStatus.Completed, (await db.ExamCandidates.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_finished_turn_shows_its_probes_in_the_history()
    {
        var clock = new VoiceRoomTests.Clock();
        await using var db = await SeedAsync(clock);
        var analyzer = new StubAnalyzer(Probe);
        var probed = await Service(db, clock, analyzer).SyncAsync(1, 2, Submit(1, "Có khoá."));
        var followUpId = probed.Data!.Question!.FollowUpId!.Value;

        clock.At = clock.At.AddSeconds(2);
        db.ChangeTracker.Clear();
        analyzer.Decision = FollowUpDecision.None;
        var state = await Service(db, clock, analyzer).SyncAsync(1, 2,
            new VoiceAnswerRequest(1, "Trả lời thêm.", Submit: true, FollowUpId: followUpId));

        var turn = Assert.Single(state.Data!.SavedAnswers!);
        var probe = Assert.Single(turn.FollowUps!);
        Assert.Equal(Probe.Question, probe.Text);
        Assert.Equal("Trả lời thêm.", probe.Transcript);
        Assert.Equal(FollowUpReason.MissingPoint, probe.Reason);
    }
}
