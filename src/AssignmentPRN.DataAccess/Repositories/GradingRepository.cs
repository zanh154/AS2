using System.Data;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>One criterion of the rubric attached to a question.</summary>
public sealed record GradingCriterionRow(int CriteriaId, string Name, string? Description, decimal MaxScore);

/// <summary>A probe asked on an answer and what the student said back.</summary>
public sealed record GradingProbeRow(int FollowUpId, string QuestionText, string? AnswerTranscript);

/// <summary>Everything needed to mark one answer, read once.</summary>
public sealed record GradingAnswerRow(
    int AnswerId,
    int ExamQuestionId,
    int OrderNo,
    bool IsFollowUpQuestion,
    string QuestionText,
    string? ExpectedAnswer,
    string? Transcript,
    IReadOnlyList<GradingCriterionRow> Criteria,
    IReadOnlyList<GradingProbeRow> Probes,
    bool AlreadyGraded);

/// <summary>A candidate's slot as the grader needs to see it.</summary>
public sealed record GradingSlotRow(int CandidateId, int ExamId, CandidateStatus Status, IReadOnlyList<GradingAnswerRow> Answers);

/// <summary>What the grader decided for one answer, ready to be stored.</summary>
public sealed record AnswerEvaluation(
    int AnswerId,
    decimal AiScore,
    decimal? Confidence,
    string? Feedback,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    IReadOnlyList<(int CriteriaId, decimal Score, string? Comment)> Criteria);

/// <summary>One criterion as the examiner reads it back: what the AI gave, out of what.</summary>
public sealed record ReviewCriterionRow(int CriteriaId, string Name, decimal Awarded, decimal MaxScore, string? Comment);

/// <summary>What the AI said about one answer, with the probes it was judged alongside.</summary>
public sealed record ReviewAnswerRow(
    int AnswerId,
    int ExamQuestionId,
    int OrderNo,
    string QuestionText,
    string? Transcript,
    decimal? AiScore,
    decimal? Confidence,
    string? Feedback,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    IReadOnlyList<ReviewCriterionRow> Criteria,
    IReadOnlyList<GradingProbeRow> Probes);

/// <summary>The AI's side of one candidate's paper, plus where the mark stands.</summary>
public sealed record ReviewRow(
    int CandidateId,
    int ExamId,
    decimal? AiScore,
    decimal? LecturerScore,
    decimal? FinalScore,
    string? LecturerComment,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    IReadOnlyList<ReviewAnswerRow> Answers);

/// <summary>One line of the trail left behind by a mark that was changed.</summary>
public sealed record ResultAuditRow(string? ByName, DateTime At, string Description);

public interface IGradingRepository
{
    /// <summary>What the AI suggested for one candidate, or null when they do not exist.</summary>
    Task<ReviewRow?> LoadReviewAsync(int candidateId, CancellationToken ct = default);

    /// <summary>
    /// Stores the mark a lecturer decided and writes the change to exam_logs, in one
    /// transaction. Passing a null score hands the slot back to the AI suggestion.
    /// </summary>
    Task AdjustAsync(int candidateId, int examId, int lecturerId, decimal? score, decimal? finalScore,
        string? comment, string auditLine, DateTime at, CancellationToken ct = default);

    /// <summary>The trail of mark changes for one candidate, newest first.</summary>
    Task<IReadOnlyList<ResultAuditRow>> AuditTrailAsync(int candidateId, CancellationToken ct = default);
    /// <summary>Null when the candidate does not exist.</summary>
    Task<GradingSlotRow?> LoadAsync(int candidateId, CancellationToken ct = default);

    /// <summary>
    /// Writes the suggestions for one slot in one transaction: an evaluation per answer
    /// with its per-criterion marks, and the slot's suggested mark on final_results.
    /// Grading again replaces what was there; a mark the lecturer decided is never
    /// touched.
    /// </summary>
    Task SaveAsync(int candidateId, int examId, IReadOnlyList<AnswerEvaluation> evaluations,
        decimal? slotScore, DateTime at, CancellationToken ct = default);

    /// <summary>Slots that finished but were never marked, oldest first.</summary>
    Task<IReadOnlyList<int>> PendingCandidateIdsAsync(CancellationToken ct = default);
}

public sealed class GradingRepository(AivesDbContext db) : IGradingRepository
{
    public async Task<GradingSlotRow?> LoadAsync(int candidateId, CancellationToken ct = default)
    {
        var candidate = await db.ExamCandidates.AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .Select(x => new { x.CandidateId, x.ExamId, x.Status })
            .SingleOrDefaultAsync(ct);
        if (candidate is null) return null;

        var answers = await db.Answers.AsNoTracking()
            .Where(answer => answer.CandidateId == candidateId)
            .Select(answer => new
            {
                answer.AnswerId,
                answer.ExamQuestionId,
                answer.Transcript,
                Question = db.ExamQuestions
                    .Where(x => x.ExamQuestionId == answer.ExamQuestionId)
                    .Select(x => new { x.OrderNo, x.QuestionId, x.ParentExamQuestionId, x.Question.QuestionText, x.Question.ExpectedAnswer })
                    .FirstOrDefault(),
                Graded = db.AiEvaluations.Any(x => x.AnswerId == answer.AnswerId)
            })
            .ToListAsync(ct);

        var questionIds = answers.Where(x => x.Question != null).Select(x => x.Question!.QuestionId).Distinct().ToList();
        var criteria = (await db.Rubrics.AsNoTracking()
                .Where(rubric => questionIds.Contains(rubric.QuestionId))
                .Select(rubric => new
                {
                    rubric.QuestionId,
                    Criteria = rubric.Criteria
                        .OrderBy(x => x.DisplayOrder)
                        .Select(x => new GradingCriterionRow(x.CriteriaId, x.CriteriaName, x.Description, x.MaxScore))
                        .ToList()
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.QuestionId, x => (IReadOnlyList<GradingCriterionRow>)x.Criteria);

        var answerIds = answers.Select(x => x.AnswerId).ToList();
        var probes = (await db.FollowUpQuestions.AsNoTracking()
                .Where(x => answerIds.Contains(x.AnswerId))
                .OrderBy(x => x.OrderNo).ThenBy(x => x.FollowUpId)
                .Select(x => new { x.AnswerId, Probe = new GradingProbeRow(x.FollowUpId, x.QuestionText, x.AnswerTranscript) })
                .ToListAsync(ct))
            .GroupBy(x => x.AnswerId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<GradingProbeRow>)group.Select(x => x.Probe).ToList());

        var rows = answers
            .Where(x => x.Question != null)
            .OrderBy(x => x.Question!.OrderNo)
            .Select(x => new GradingAnswerRow(
                x.AnswerId,
                x.ExamQuestionId,
                x.Question!.OrderNo,
                x.Question.ParentExamQuestionId != null,
                x.Question.QuestionText,
                x.Question.ExpectedAnswer,
                x.Transcript,
                criteria.TryGetValue(x.Question.QuestionId, out var list) ? list : [],
                probes.TryGetValue(x.AnswerId, out var asked) ? asked : [],
                x.Graded))
            .ToList();

        return new GradingSlotRow(candidate.CandidateId, candidate.ExamId, candidate.Status, rows);
    }

    public async Task SaveAsync(int candidateId, int examId, IReadOnlyList<AnswerEvaluation> evaluations,
        decimal? slotScore, DateTime at, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evaluations);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        db.ChangeTracker.Clear();

        foreach (var evaluation in evaluations)
        {
            var existing = await db.AiEvaluations
                .Include(x => x.Criteria)
                .SingleOrDefaultAsync(x => x.AnswerId == evaluation.AnswerId, ct);

            if (existing is null)
            {
                existing = new AiEvaluation { AnswerId = evaluation.AnswerId };
                db.AiEvaluations.Add(existing);
            }
            else
            {
                // Grading again replaces the previous marks rather than piling up a
                // second opinion beside them.
                db.AiEvaluationCriteria.RemoveRange(existing.Criteria);
                existing.Criteria.Clear();
            }

            existing.AiScore = evaluation.AiScore;
            existing.Confidence = evaluation.Confidence;
            existing.Feedback = evaluation.Feedback;
            existing.Strengths = evaluation.Strengths;
            existing.Weaknesses = evaluation.Weaknesses;
            existing.MissingPoints = evaluation.MissingPoints;
            existing.EvaluatedAt = at;

            foreach (var (criteriaId, score, comment) in evaluation.Criteria)
            {
                existing.Criteria.Add(new AiEvaluationCriteria
                {
                    CriteriaId = criteriaId,
                    Score = score,
                    Comment = comment
                });
            }
        }

        var result = await db.FinalResults.SingleOrDefaultAsync(x => x.CandidateId == candidateId, ct);
        if (result is null)
        {
            result = new FinalResult { ExamId = examId, CandidateId = candidateId };
            db.FinalResults.Add(result);
        }

        result.AiScore = slotScore;
        // The lecturer's decision stands. Only a slot nobody has marked by hand takes
        // the suggestion as its final score.
        if (result.LecturerScore is null) result.FinalScore = slotScore;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>The action written on every mark change, so the trail can be read back.</summary>
    public const string AdjustedAction = "result.adjusted";

    public async Task<ReviewRow?> LoadReviewAsync(int candidateId, CancellationToken ct = default)
    {
        var candidate = await db.ExamCandidates.AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .Select(x => new { x.CandidateId, x.ExamId })
            .SingleOrDefaultAsync(ct);
        if (candidate is null) return null;

        var answers = await db.Answers.AsNoTracking()
            .Where(answer => answer.CandidateId == candidateId)
            .Select(answer => new
            {
                answer.AnswerId,
                answer.ExamQuestionId,
                answer.Transcript,
                Question = db.ExamQuestions
                    .Where(x => x.ExamQuestionId == answer.ExamQuestionId)
                    .Select(x => new { x.OrderNo, x.Question.QuestionText })
                    .FirstOrDefault(),
                Evaluation = db.AiEvaluations
                    .Where(x => x.AnswerId == answer.AnswerId)
                    .Select(x => new
                    {
                        x.AiScore,
                        x.Confidence,
                        x.Feedback,
                        x.Strengths,
                        x.Weaknesses,
                        x.MissingPoints,
                        Criteria = x.Criteria
                            .Select(mark => new ReviewCriterionRow(mark.CriteriaId, mark.Criteria.CriteriaName,
                                mark.Score, mark.Criteria.MaxScore, mark.Comment))
                            .ToList()
                    })
                    .FirstOrDefault(),
                Probes = db.FollowUpQuestions
                    .Where(x => x.AnswerId == answer.AnswerId)
                    .OrderBy(x => x.OrderNo).ThenBy(x => x.FollowUpId)
                    .Select(x => new GradingProbeRow(x.FollowUpId, x.QuestionText, x.AnswerTranscript))
                    .ToList()
            })
            .ToListAsync(ct);

        var result = await db.FinalResults.AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .Select(x => new
            {
                x.AiScore,
                x.LecturerScore,
                x.FinalScore,
                x.LecturerComment,
                x.FinalizedAt,
                FinalizedByName = db.Users.Where(user => user.UserId == x.FinalizedBy).Select(user => user.FullName).FirstOrDefault()
            })
            .SingleOrDefaultAsync(ct);

        return new ReviewRow(
            candidate.CandidateId,
            candidate.ExamId,
            result?.AiScore,
            result?.LecturerScore,
            result?.FinalScore,
            result?.LecturerComment,
            result?.FinalizedByName,
            result?.FinalizedAt,
            answers
                .Where(x => x.Question != null)
                .OrderBy(x => x.Question!.OrderNo)
                .Select(x => new ReviewAnswerRow(
                    x.AnswerId,
                    x.ExamQuestionId,
                    x.Question!.OrderNo,
                    x.Question.QuestionText,
                    x.Transcript,
                    x.Evaluation?.AiScore,
                    x.Evaluation?.Confidence,
                    x.Evaluation?.Feedback,
                    x.Evaluation?.Strengths,
                    x.Evaluation?.Weaknesses,
                    x.Evaluation?.MissingPoints,
                    x.Evaluation is null ? [] : x.Evaluation.Criteria,
                    x.Probes))
                .ToList());
    }

    public async Task AdjustAsync(int candidateId, int examId, int lecturerId, decimal? score, decimal? finalScore,
        string? comment, string auditLine, DateTime at, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        db.ChangeTracker.Clear();

        var result = await db.FinalResults.SingleOrDefaultAsync(x => x.CandidateId == candidateId, ct);
        if (result is null)
        {
            result = new FinalResult { ExamId = examId, CandidateId = candidateId };
            db.FinalResults.Add(result);
        }

        result.LecturerScore = score;
        result.FinalScore = finalScore;
        result.LecturerComment = comment;
        result.FinalizedBy = lecturerId;
        result.FinalizedAt = at;

        // The row only keeps the mark that counts now; the log is what makes an earlier
        // mark traceable when a student questions their grade.
        db.Set<ExamLog>().Add(new ExamLog
        {
            ExamId = examId,
            CandidateId = candidateId,
            UserId = lecturerId,
            Action = AdjustedAction,
            Description = auditLine,
            Timestamp = at
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ResultAuditRow>> AuditTrailAsync(int candidateId, CancellationToken ct = default) =>
        await db.Set<ExamLog>().AsNoTracking()
            .Where(log => log.CandidateId == candidateId && log.Action == AdjustedAction)
            .OrderByDescending(log => log.Timestamp).ThenByDescending(log => log.LogId)
            .Select(log => new ResultAuditRow(
                db.Users.Where(user => user.UserId == log.UserId).Select(user => user.FullName).FirstOrDefault(),
                log.Timestamp,
                log.Description ?? string.Empty))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> PendingCandidateIdsAsync(CancellationToken ct = default) =>
        await db.ExamCandidates.AsNoTracking()
            .Where(candidate => candidate.Status == CandidateStatus.Completed
                && db.Answers.Any(answer => answer.CandidateId == candidate.CandidateId
                    && !db.AiEvaluations.Any(evaluation => evaluation.AnswerId == answer.AnswerId)))
            .OrderBy(candidate => candidate.FinishedAt)
            .ThenBy(candidate => candidate.CandidateId)
            .Select(candidate => candidate.CandidateId)
            .ToListAsync(ct);
}
