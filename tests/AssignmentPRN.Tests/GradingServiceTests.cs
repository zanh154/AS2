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
/// Grading a finished slot end to end, with the model stubbed. The point of these is
/// what reaches the database: a suggestion the examiner can act on, never a score that
/// looks earned when nothing was judged.
/// </summary>
public sealed class GradingServiceTests
{
    private sealed class StubGrader(AnswerGrade grade) : IAnswerGrader
    {
        public int Calls { get; private set; }
        public List<GradingContext> Seen { get; } = [];
        public AnswerGrade Grade { get; set; } = grade;

        public Task<AnswerGrade> GradeAsync(GradingContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            Seen.Add(context);
            return Task.FromResult(Grade);
        }
    }

    private sealed class ThrowingGrader : IAnswerGrader
    {
        public Task<AnswerGrade> GradeAsync(GradingContext context, CancellationToken cancellationToken = default) =>
            Task.FromException<AnswerGrade>(new HttpRequestException("model is down"));
    }

    /// <summary>Records what the examiner's screen would have been told.</summary>
    private sealed class RecordingNotifier : IGradingNotifier
    {
        public List<(int ExamId, int CandidateId, int Done, int Total)> Progress { get; } = [];
        public List<GradingOutcome> Finished { get; } = [];

        void IGradingNotifier.Progress(GradingSlotRow slot, int answersDone) =>
            Progress.Add((slot.ExamId, slot.CandidateId, answersDone, slot.Answers.Count));

        void IGradingNotifier.Finished(GradingSlotRow slot, GradingOutcome outcome) => Finished.Add(outcome);
    }

    /// <summary>A notifier whose transport is broken.</summary>
    private sealed class ThrowingNotifier : IGradingNotifier
    {
        public void Progress(GradingSlotRow slot, int answersDone) => throw new InvalidOperationException("hub is gone");

        public void Finished(GradingSlotRow slot, GradingOutcome outcome) => throw new InvalidOperationException("hub is gone");
    }

    private static GradingService Service(AivesDbContext db, IAnswerGrader grader, IGradingNotifier? notifier = null) =>
        new(new GradingRepository(db), grader, TimeProvider.System, notifier);

    /// <summary>A finished slot: one question with a 6 + 4 rubric and a spoken answer.</summary>
    private static async Task<AivesDbContext> SeedAsync(bool withRubric = true, bool completed = true, string? transcript = "Em trả lời.")
    {
        var clock = new VoiceRoomTests.Clock();
        var db = await VoiceRoomTests.SeedAsync(clock);
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.Status = completed ? CandidateStatus.Completed : CandidateStatus.InProgress;
        candidate.FinishedAt = clock.At.LocalDateTime;

        var first = await db.ExamQuestions.SingleAsync(x => x.OrderNo == 1);
        db.Answers.Add(new Answer { ExamQuestionId = first.ExamQuestionId, CandidateId = 1, Transcript = transcript,
            CreatedAt = clock.At.LocalDateTime, FinishedAt = clock.At.LocalDateTime });

        if (withRubric)
        {
            db.Rubrics.Add(new Rubric
            {
                RubricId = 1, QuestionId = first.QuestionId, TotalScore = 10m,
                Criteria =
                [
                    new RubricCriteria { CriteriaId = 1, CriteriaName = "Đúng khái niệm", MaxScore = 6m, DisplayOrder = 1 },
                    new RubricCriteria { CriteriaId = 2, CriteriaName = "Ví dụ", MaxScore = 4m, DisplayOrder = 2 }
                ]
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static AnswerGrade FullMarks => new(
        [new CriterionMark(1, 6m, "Đúng"), new CriterionMark(2, 4m, "Rõ")],
        "Nắm chắc", "Không có", null, 0.9m);

    [Fact]
    public async Task A_graded_answer_stores_a_mark_for_every_criterion()
    {
        await using var db = await SeedAsync();

        var result = await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.GradedAnswers);
        Assert.Equal(10m, result.Data.SlotScore);

        db.ChangeTracker.Clear();
        var evaluation = await db.AiEvaluations.Include(x => x.Criteria).SingleAsync();
        Assert.Equal(10m, evaluation.AiScore);
        Assert.Equal(0.9m, evaluation.Confidence);
        Assert.Equal("Nắm chắc", evaluation.Strengths);
        Assert.Equal(2, evaluation.Criteria.Count);
        Assert.Equal([6m, 4m], evaluation.Criteria.OrderBy(x => x.CriteriaId).Select(x => x.Score));
    }

    [Fact]
    public async Task The_slot_score_lands_on_the_result_row_as_a_suggestion()
    {
        await using var db = await SeedAsync();

        await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);

        db.ChangeTracker.Clear();
        var result = await db.FinalResults.SingleAsync();
        Assert.Equal(10m, result.AiScore);
        Assert.Equal(10m, result.FinalScore);
        Assert.Null(result.LecturerScore);
        Assert.Null(result.FinalizedBy);
    }

    [Fact]
    public async Task A_mark_the_lecturer_decided_is_never_overwritten_by_grading_again()
    {
        await using var db = await SeedAsync();
        await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);

        db.ChangeTracker.Clear();
        var result = await db.FinalResults.SingleAsync();
        result.LecturerScore = 7m;
        result.FinalScore = 7m;
        result.FinalizedBy = 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Service(db, new StubGrader(new AnswerGrade([new CriterionMark(1, 0m, null), new CriterionMark(2, 0m, null)],
            null, null, null, null))).GradeCandidateAsync(1);

        db.ChangeTracker.Clear();
        var after = await db.FinalResults.SingleAsync();
        Assert.Equal(0m, after.AiScore);
        Assert.Equal(7m, after.FinalScore);
        Assert.Equal(7m, after.LecturerScore);
    }

    [Fact]
    public async Task Grading_again_replaces_the_previous_marks_rather_than_adding_to_them()
    {
        await using var db = await SeedAsync();
        await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);
        db.ChangeTracker.Clear();

        await Service(db, new StubGrader(new AnswerGrade([new CriterionMark(1, 3m, "lại")], null, null, null, null)))
            .GradeCandidateAsync(1);

        db.ChangeTracker.Clear();
        var evaluation = await db.AiEvaluations.Include(x => x.Criteria).SingleAsync();
        Assert.Equal(2, evaluation.Criteria.Count);
        Assert.Equal(3m, evaluation.Criteria.Single(x => x.CriteriaId == 1).Score);
        // The criterion the second run skipped is zero, and says so.
        Assert.Equal(RubricScoring.NotMarkedComment, evaluation.Criteria.Single(x => x.CriteriaId == 2).Comment);
    }

    [Fact]
    public async Task A_partial_marking_tells_the_examiner_which_part_is_missing()
    {
        await using var db = await SeedAsync();

        var result = await Service(db, new StubGrader(
            new AnswerGrade([new CriterionMark(1, 6m, "Đúng")], null, null, null, null))).GradeCandidateAsync(1);

        Assert.Equal(1, result.Data!.UnmarkedCriteria);
        db.ChangeTracker.Clear();
        var evaluation = await db.AiEvaluations.SingleAsync();
        Assert.Contains("chưa đánh giá 1 tiêu chí", evaluation.Feedback, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6m, evaluation.AiScore);
    }

    [Fact]
    public async Task An_answer_with_no_rubric_is_left_for_the_examiner_rather_than_scored_zero()
    {
        await using var db = await SeedAsync(withRubric: false);
        var grader = new StubGrader(AnswerGrade.None);

        var result = await Service(db, grader).GradeCandidateAsync(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.SkippedAnswers);
        Assert.Null(result.Data.SlotScore);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.AiEvaluations.ToListAsync());
        // No suggestion means no result row claiming one.
        Assert.Empty(await db.FinalResults.ToListAsync());
    }

    [Fact]
    public async Task A_model_that_is_down_leaves_the_slot_unmarked_without_failing()
    {
        await using var db = await SeedAsync();

        var result = await Service(db, new ThrowingGrader()).GradeCandidateAsync(1);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.SkippedAnswers);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.AiEvaluations.ToListAsync());
    }

    [Fact]
    public async Task A_partial_retry_does_not_replace_the_existing_total()
    {
        await using var db = await SeedAsync();
        await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);
        db.ChangeTracker.Clear();

        await Service(db, new ThrowingGrader()).GradeCandidateAsync(1);

        db.ChangeTracker.Clear();
        var result = await db.FinalResults.SingleAsync();
        Assert.Equal(10m, result.AiScore);
        Assert.Equal(10m, result.FinalScore);
    }

    [Fact]
    public async Task A_slot_still_being_sat_is_refused()
    {
        // Marking half a paper would hand the examiner a score the student never finished.
        await using var db = await SeedAsync(completed: false);

        var result = await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);

        Assert.False(result.Success);
        Assert.Contains("hoàn thành", result.Error);
    }

    [Fact]
    public async Task A_slot_that_does_not_exist_is_refused()
    {
        await using var db = await SeedAsync();

        Assert.False((await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(999)).Success);
        Assert.False((await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(0)).Success);
    }

    [Fact]
    public async Task The_grader_is_shown_the_probes_asked_on_the_answer()
    {
        await using var db = await SeedAsync();
        var answer = await db.Answers.SingleAsync();
        db.FollowUpQuestions.Add(new FollowUpQuestion { AnswerId = answer.AnswerId, QuestionText = "Nói rõ hơn?",
            AnswerTranscript = "Em nói thêm.", OrderNo = 1, AskedAt = DateTime.Now, AnsweredAt = DateTime.Now });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var grader = new StubGrader(FullMarks);
        await Service(db, grader).GradeCandidateAsync(1);

        var exchange = Assert.Single(grader.Seen[0].FollowUps);
        Assert.Equal("Nói rõ hơn?", exchange.Question);
        Assert.Equal("Em nói thêm.", exchange.Transcript);
    }

    [Fact]
    public async Task The_grader_is_shown_the_rubric_and_the_expected_answer()
    {
        await using var db = await SeedAsync();
        var question = await db.Questions.SingleAsync(x => x.QuestionId == 1);
        question.ExpectedAnswer = "Đáp án mong đợi.";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var grader = new StubGrader(FullMarks);
        await Service(db, grader).GradeCandidateAsync(1);

        Assert.Equal("Đáp án mong đợi.", grader.Seen[0].ExpectedAnswer);
        Assert.Equal([1, 2], grader.Seen[0].Criteria.Select(x => x.CriteriaId));
        Assert.Equal(6m, grader.Seen[0].Criteria[0].MaxScore);
    }

    [Fact]
    public async Task The_examiner_is_told_how_the_marking_is_going_and_how_it_ended()
    {
        await using var db = await SeedAsync();
        var notifier = new RecordingNotifier();

        await Service(db, new StubGrader(FullMarks), notifier).GradeCandidateAsync(1);

        var progress = Assert.Single(notifier.Progress);
        Assert.Equal((1, 1, 1, 1), progress);
        var finished = Assert.Single(notifier.Finished);
        Assert.Equal(1, finished.GradedAnswers);
        Assert.Equal(10m, finished.SlotScore);
    }

    [Fact]
    public async Task An_answer_left_unmarked_still_moves_the_progress_along()
    {
        // Otherwise a slot the model could not mark would look stuck on the examiner's
        // screen for as long as grading ran.
        await using var db = await SeedAsync();
        var notifier = new RecordingNotifier();

        await Service(db, new ThrowingGrader(), notifier).GradeCandidateAsync(1);

        Assert.Single(notifier.Progress);
        Assert.Equal(1, Assert.Single(notifier.Finished).SkippedAnswers);
    }

    [Fact]
    public async Task A_notification_that_fails_cannot_undo_a_stored_mark()
    {
        await using var db = await SeedAsync();

        var result = await Service(db, new StubGrader(FullMarks), new ThrowingNotifier()).GradeCandidateAsync(1);

        Assert.False(result.Success, "the failure is reported rather than hidden");
        db.ChangeTracker.Clear();
        // What was already written stays written: grading again is safe and cheap.
        Assert.Empty(await db.AiEvaluations.ToListAsync());
    }

    [Fact]
    public async Task A_slot_the_lecturer_closes_is_queued_for_marking()
    {
        // Nobody handed this one in, so nothing queued it when it finished. Without the
        // close doing it, the slot would wait for the next restart to be swept up.
        await using var db = await SeedAsync(completed: false);
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.Status = AssignmentPRN.DataAccess.Enums.CandidateStatus.InProgress;
        candidate.ScheduledTime = DateTime.Now.AddHours(-2);
        var exam = await db.ExamSessions.SingleAsync();
        exam.StartTime = candidate.ScheduledTime.Value;
        exam.EndTime = DateTime.Now.AddHours(-1);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var queue = new AssignmentPRN.Business.GradingQueue();
        var service = new ExamResultService(new ExamResultRepository(db, new ExamStatePolicy()),
            new QuestionRepository(db, new ExamStatePolicy()), null, null, queue);

        var closed = await service.CloseOverdueSlotsAsync(1, lecturerId: null);

        Assert.True(closed.Success, closed.Error);
        Assert.Equal(1, closed.Data!.Completed);
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var queued in queue.DequeueAsync(cancel.Token))
        {
            Assert.Equal(1, queued);
            break;
        }
    }

    [Fact]
    public async Task A_finished_slot_that_was_never_marked_is_found_again()
    {
        await using var db = await SeedAsync();

        var pending = await new GradingRepository(db).PendingCandidateIdsAsync();
        Assert.Equal([1], pending);

        await Service(db, new StubGrader(FullMarks)).GradeCandidateAsync(1);
        db.ChangeTracker.Clear();

        Assert.Empty(await new GradingRepository(db).PendingCandidateIdsAsync());
    }
}
