using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;

namespace AssignmentPRN.Tests;

/// <summary>
/// The human-in-the-loop half: the examiner reads what the AI suggested and decides the
/// mark. The point of these is that the decision is theirs and stays theirs, and that a
/// mark that was overwritten can still be traced when a student questions their grade.
/// </summary>
public sealed class ResultReviewTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(0d, true)]
    [InlineData(10d, true)]
    [InlineData(7.25, true)]
    [InlineData(-0.1, false)]
    [InlineData(10.1, false)]
    [InlineData(7.125, false)]
    public void A_score_outside_the_scale_is_refused(double? score, bool expected) =>
        Assert.Equal(expected, ResultAdjustmentRules.IsValidScore((decimal?)score));

    [Fact]
    public void The_lecturer_mark_is_the_one_that_counts()
    {
        Assert.Equal(7m, ResultAdjustmentRules.FinalScore(7m, 9m));
        Assert.Equal(9m, ResultAdjustmentRules.FinalScore(null, 9m));
        Assert.Null(ResultAdjustmentRules.FinalScore(null, null));
    }

    [Fact]
    public void The_audit_line_names_both_marks()
    {
        Assert.Equal("Điểm chốt: 9 → 7", ResultAdjustmentRules.AuditLine(9m, 7m, null));
        Assert.Equal("Điểm chốt: chưa chốt → 7.5", ResultAdjustmentRules.AuditLine(null, 7.5m, "   "));
        Assert.Equal("Điểm chốt: 7 → chưa chốt. Nhận xét: nghe lại", ResultAdjustmentRules.AuditLine(7m, null, " nghe lại "));
    }

    [Fact]
    public void A_very_long_comment_is_cut_to_fit_the_log()
    {
        var line = ResultAdjustmentRules.AuditLine(5m, 6m, new string('a', 3000));

        Assert.True(line.Length < 2000, "exam_logs.description holds 2000 characters");
    }

    private static async Task<AivesDbContext> SeedAsync(decimal? aiScore = 8m)
    {
        var clock = new VoiceRoomTests.Clock();
        var db = await VoiceRoomTests.SeedAsync(clock);
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.Status = CandidateStatus.Completed;
        candidate.FinishedAt = clock.At.LocalDateTime;

        var first = await db.ExamQuestions.SingleAsync(x => x.OrderNo == 1);
        var answer = new Answer { ExamQuestionId = first.ExamQuestionId, CandidateId = 1, Transcript = "Em trả lời.",
            CreatedAt = clock.At.LocalDateTime, FinishedAt = clock.At.LocalDateTime };
        db.Answers.Add(answer);
        db.Rubrics.Add(new Rubric { RubricId = 1, QuestionId = first.QuestionId, TotalScore = 10m,
            Criteria = [new RubricCriteria { CriteriaId = 1, CriteriaName = "Đúng khái niệm", MaxScore = 10m, DisplayOrder = 1 }] });
        await db.SaveChangesAsync();

        if (aiScore is not null)
        {
            db.AiEvaluations.Add(new AiEvaluation
            {
                AnswerId = answer.AnswerId, AiScore = aiScore.Value, Confidence = 0.7m,
                Strengths = "Nắm ý chính", Weaknesses = "Thiếu ví dụ", EvaluatedAt = clock.At.LocalDateTime,
                Criteria = [new AiEvaluationCriteria { CriteriaId = 1, Score = aiScore.Value, Comment = "Khá" }]
            });
            db.FinalResults.Add(new FinalResult { ExamId = 1, CandidateId = 1, AiScore = aiScore, FinalScore = aiScore });
            await db.SaveChangesAsync();
        }

        db.ChangeTracker.Clear();
        return db;
    }

    private static ResultReviewService Service(AivesDbContext db) => new(
        new GradingRepository(db),
        new ExamResultService(new ExamResultRepository(db, new ExamStatePolicy()),
            new QuestionRepository(db, new ExamStatePolicy())),
        TimeProvider.System);

    [Fact]
    public async Task The_examiner_reads_the_marks_criterion_by_criterion()
    {
        await using var db = await SeedAsync();

        var result = await Service(db).GetAsync(1, lecturerId: 1);

        Assert.True(result.Success, result.Error);
        var answer = Assert.Single(result.Data!.Answers);
        Assert.Equal(8m, answer.AiScore);
        Assert.False(answer.NotGraded);
        var criterion = Assert.Single(answer.Criteria);
        Assert.Equal("Đúng khái niệm", criterion.Name);
        Assert.Equal(8m, criterion.Awarded);
        Assert.Equal(10m, criterion.MaxScore);
        Assert.Equal("Nắm ý chính", answer.Strengths);
    }

    [Fact]
    public async Task An_answer_the_model_never_marked_says_so()
    {
        await using var db = await SeedAsync(aiScore: null);

        var result = await Service(db).GetAsync(1, lecturerId: 1);

        Assert.True(Assert.Single(result.Data!.Answers).NotGraded);
        Assert.Null(result.Data.AiScore);
    }

    [Fact]
    public async Task A_mark_the_lecturer_enters_becomes_the_final_score_and_is_logged()
    {
        await using var db = await SeedAsync();

        var saved = await Service(db).AdjustAsync(1, lecturerId: 1, score: 6.5m, comment: "Trả lời chưa đủ ví dụ.",
            ownerLecturerId: 1);

        Assert.True(saved.Success, saved.Error);
        db.ChangeTracker.Clear();
        var row = await db.FinalResults.SingleAsync();
        Assert.Equal(6.5m, row.LecturerScore);
        Assert.Equal(6.5m, row.FinalScore);
        Assert.Equal(8m, row.AiScore);
        Assert.Equal(1, row.FinalizedBy);
        Assert.NotNull(row.FinalizedAt);

        var log = await db.Set<ExamLog>().SingleAsync(x => x.Action == GradingRepository.AdjustedAction);
        Assert.Equal(1, log.UserId);
        Assert.Contains("8 → 6.5", log.Description);
        Assert.Contains("chưa đủ ví dụ", log.Description);
    }

    [Fact]
    public async Task Changing_the_mark_again_leaves_both_changes_in_the_trail()
    {
        await using var db = await SeedAsync();
        await Service(db).AdjustAsync(1, 1, 6.5m, null, 1);
        db.ChangeTracker.Clear();
        await Service(db).AdjustAsync(1, 1, 9m, "Nghe lại bản thu.", 1);
        db.ChangeTracker.Clear();

        var review = await Service(db).GetAsync(1, 1);

        // The row keeps only the mark that counts now; the trail is what makes the
        // earlier one answerable.
        Assert.Equal(9m, review.Data!.FinalScore);
        Assert.Equal(2, review.Data.Trail.Count);
        Assert.Contains("6.5 → 9", review.Data.Trail[0].Description);
        Assert.Contains("8 → 6.5", review.Data.Trail[1].Description);
    }

    [Fact]
    public async Task Clearing_the_mark_hands_the_slot_back_to_the_suggestion()
    {
        await using var db = await SeedAsync();
        await Service(db).AdjustAsync(1, 1, 6.5m, null, 1);
        db.ChangeTracker.Clear();

        var cleared = await Service(db).AdjustAsync(1, 1, null, null, 1);

        Assert.True(cleared.Success, cleared.Error);
        db.ChangeTracker.Clear();
        var row = await db.FinalResults.SingleAsync();
        Assert.Null(row.LecturerScore);
        Assert.Equal(8m, row.FinalScore);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10.5)]
    [InlineData(1.234)]
    public async Task A_score_off_the_scale_is_refused_before_anything_is_written(double score)
    {
        await using var db = await SeedAsync();

        var result = await Service(db).AdjustAsync(1, 1, (decimal)score, null, 1);

        Assert.False(result.Success);
        db.ChangeTracker.Clear();
        Assert.Null((await db.FinalResults.SingleAsync()).LecturerScore);
        Assert.Empty(await db.Set<ExamLog>().Where(x => x.Action == GradingRepository.AdjustedAction).ToListAsync());
    }

    [Fact]
    public async Task A_lecturer_cannot_read_or_mark_another_lecturers_candidate()
    {
        await using var db = await SeedAsync();

        // Lecturer 99 runs no session here, so ownership turns them away at the service.
        Assert.False((await Service(db).GetAsync(1, lecturerId: 99)).Success);
        var denied = await Service(db).AdjustAsync(1, 99, 5m, null, ownerLecturerId: 99);

        Assert.False(denied.Success);
        db.ChangeTracker.Clear();
        Assert.Null((await db.FinalResults.SingleAsync()).LecturerScore);
    }

    [Fact]
    public async Task An_admin_may_mark_any_candidate()
    {
        // An admin has no lecturer id, which is how the results module already says
        // "every session".
        await using var db = await SeedAsync();

        var saved = await Service(db).AdjustAsync(1, lecturerId: 1, score: 5m, comment: null, ownerLecturerId: null);

        Assert.True(saved.Success, saved.Error);
    }

    [Fact]
    public async Task A_student_reads_their_own_report_once_the_slot_is_finished()
    {
        await using var db = await SeedAsync();
        await Service(db).AdjustAsync(1, 1, 7m, "Cố gắng thêm ví dụ.", 1);
        db.ChangeTracker.Clear();

        // Student 2 is the one sitting this slot in the shared fixture.
        var report = await Service(db).GetForStudentAsync(1, studentId: 2);

        Assert.True(report.Success, report.Error);
        Assert.Equal(7m, report.Data!.FinalScore);
        Assert.Equal("Cố gắng thêm ví dụ.", report.Data.LecturerComment);
        Assert.Equal(8m, Assert.Single(report.Data.Answers).AiScore);
    }

    [Fact]
    public async Task A_student_never_sees_who_changed_their_mark()
    {
        // Who marked it down and when is the examiner's record. The mark and the reason
        // for it are the student's; the trail is not.
        await using var db = await SeedAsync();
        await Service(db).AdjustAsync(1, 1, 7m, null, 1);
        db.ChangeTracker.Clear();

        var report = await Service(db).GetForStudentAsync(1, 2);

        Assert.Empty(report.Data!.Trail);
        Assert.Null(report.Data.FinalizedByName);
    }

    [Fact]
    public async Task A_student_cannot_open_someone_elses_report()
    {
        await using var db = await SeedAsync();

        var denied = await Service(db).GetForStudentAsync(1, studentId: 99);

        Assert.False(denied.Success);
        // The same message as a slot that does not exist: whether it belongs to someone
        // else is not something to let a student probe for.
        Assert.Equal((await Service(db).GetForStudentAsync(999, 99)).Error, denied.Error);
    }

    [Fact]
    public async Task A_report_is_refused_while_the_slot_is_still_being_sat()
    {
        await using var db = await SeedAsync();
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.Status = CandidateStatus.InProgress;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Service(db).GetForStudentAsync(1, 2);

        Assert.False(result.Success);
        Assert.Contains("chưa hoàn thành", result.Error);
    }

    [Fact]
    public async Task A_candidate_that_does_not_exist_is_refused()
    {
        await using var db = await SeedAsync();

        Assert.False((await Service(db).GetAsync(999, 1)).Success);
        Assert.False((await Service(db).AdjustAsync(0, 1, 5m, null, 1)).Success);
    }
}
