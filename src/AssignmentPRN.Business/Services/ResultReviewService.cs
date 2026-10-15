using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>One criterion as the examiner reads it back.</summary>
public sealed record ReviewCriterion(string Name, decimal Awarded, decimal MaxScore, string? Comment);

/// <summary>A probe and the reply it drew, shown beside the answer it belongs to.</summary>
public sealed record ReviewProbe(string Question, string? Transcript);

/// <summary>What the AI said about one answer.</summary>
public sealed record ReviewAnswer(
    int OrderNo,
    string QuestionText,
    string? Transcript,
    decimal? AiScore,
    decimal? Confidence,
    string? Feedback,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    IReadOnlyList<ReviewCriterion> Criteria,
    IReadOnlyList<ReviewProbe> Probes)
{
    /// <summary>True when the model never marked this one, so the examiner must.</summary>
    public bool NotGraded => AiScore is null;
}

/// <summary>One recorded change of the mark.</summary>
public sealed record ReviewAuditEntry(string? ByName, DateTime At, string Description);

/// <summary>The AI's suggestion for one candidate and where their mark stands.</summary>
public sealed record CandidateReview(
    int CandidateId,
    int ExamId,
    decimal? AiScore,
    decimal? LecturerScore,
    decimal? FinalScore,
    string? LecturerComment,
    string? FinalizedByName,
    DateTime? FinalizedAt,
    IReadOnlyList<ReviewAnswer> Answers,
    IReadOnlyList<ReviewAuditEntry> Trail);

public interface IResultReviewService
{
    Task<ServiceResponse<CandidateReview>> GetAsync(int candidateId, int? lecturerId, CancellationToken ct = default);

    /// <summary>
    /// Records the mark the lecturer decided. A null score takes their mark away again
    /// and hands the slot back to the AI suggestion. Either way the change is logged.
    /// </summary>
    Task<ServiceResponse> AdjustAsync(int candidateId, int lecturerId, decimal? score, string? comment,
        int? ownerLecturerId, CancellationToken ct = default);
}

/// <summary>
/// The human-in-the-loop half of the module: the examiner sees what the AI suggested,
/// criterion by criterion, and decides the mark. Ownership is checked here through
/// <see cref="IExamResultService"/>, the same way the results screens already do, so a
/// lecturer only ever reaches a session they run.
/// </summary>
public sealed class ResultReviewService(
    IGradingRepository repository,
    IExamResultService results,
    TimeProvider clock) : IResultReviewService
{
    public Task<ServiceResponse<CandidateReview>> GetAsync(int candidateId, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var id = BusinessValidation.PositiveId(candidateId, "lượt thi");
            await EnsureOwnedAsync(id, lecturerId, ct);

            var review = await repository.LoadReviewAsync(id, ct)
                ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");
            var trail = await repository.AuditTrailAsync(id, ct);

            return new CandidateReview(
                review.CandidateId,
                review.ExamId,
                review.AiScore,
                review.LecturerScore,
                review.FinalScore,
                review.LecturerComment,
                review.FinalizedByName,
                review.FinalizedAt,
                review.Answers.Select(answer => new ReviewAnswer(
                    answer.OrderNo,
                    answer.QuestionText,
                    answer.Transcript,
                    answer.AiScore,
                    answer.Confidence,
                    answer.Feedback,
                    answer.Strengths,
                    answer.Weaknesses,
                    answer.MissingPoints,
                    answer.Criteria
                        .Select(x => new ReviewCriterion(x.Name, x.Awarded, x.MaxScore, x.Comment))
                        .ToArray(),
                    answer.Probes.Select(x => new ReviewProbe(x.QuestionText, x.AnswerTranscript)).ToArray()))
                    .ToArray(),
                trail.Select(x => new ReviewAuditEntry(x.ByName, x.At, x.Description)).ToArray());
        }, "Không thể tải đánh giá của AI.");

    public Task<ServiceResponse> AdjustAsync(int candidateId, int lecturerId, decimal? score, string? comment,
        int? ownerLecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var id = BusinessValidation.PositiveId(candidateId, "lượt thi");
            BusinessValidation.PositiveId(lecturerId, "người chấm");
            if (!ResultAdjustmentRules.IsValidScore(score))
                throw new BusinessValidationException("Điểm phải từ 0 đến 10, tối đa hai chữ số thập phân.");
            if (comment?.Length > 2000)
                throw new BusinessValidationException("Nhận xét quá dài.");

            var candidate = await EnsureOwnedAsync(id, ownerLecturerId, ct);
            var review = await repository.LoadReviewAsync(id, ct)
                ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

            var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            var final = ResultAdjustmentRules.FinalScore(score, review.AiScore);
            await repository.AdjustAsync(id, candidate, lecturerId, score, final, trimmed,
                ResultAdjustmentRules.AuditLine(review.FinalScore, final, trimmed),
                clock.GetLocalNow().DateTime, ct);
        }, "Không thể lưu điểm.");

    /// <summary>Returns the exam the candidate sits, once the caller is allowed to see it.</summary>
    private async Task<int> EnsureOwnedAsync(int candidateId, int? lecturerId, CancellationToken ct)
    {
        var paper = await results.GetCandidateResultAsync(candidateId, lecturerId, ct);
        if (!paper.Success || paper.Data is null)
            throw new BusinessValidationException(paper.Error ?? "Không tìm thấy lượt thi.");
        return paper.Data.ExamId;
    }
}
