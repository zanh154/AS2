using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// The review desk for AI-generated questions. A lecturer only decides on the courses
/// they teach; the check lives here rather than in the page, so hiding a button never
/// grants access. A manual question is approved at creation and never reaches this desk.
/// </summary>
public class QuestionReviewService(
    IQuestionRepository questionRepository,
    ICatalogRepository catalogRepository) : IQuestionReviewService
{
    public Task<ServiceResponse<IReadOnlyList<QuestionListItemResponse>>> ListPendingAsync(
        int? lecturerId,
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<QuestionListItemResponse>>(
            async () =>
            {
                var allowedCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (allowedCourseIds.Count == 0)
                {
                    return (IReadOnlyList<QuestionListItemResponse>)Array.Empty<QuestionListItemResponse>();
                }

                if (courseId is int requested && !allowedCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem câu hỏi của môn học này.");
                }

                var effectiveCourseId = courseId ?? (allowedCourseIds.Count == 1 ? allowedCourseIds[0] : null);

                var items = await questionRepository.ListAsync(
                    new QuestionQuery
                    {
                        CourseId = effectiveCourseId,
                        Status = AssignmentPRN.DataAccess.Enums.QuestionStatus.PendingReview
                    },
                    cancellationToken);

                return items
                    .Where(item => allowedCourseIds.Contains(item.CourseId))
                    .Select(item => new QuestionListItemResponse
                    {
                        QuestionId = item.QuestionId,
                        CourseId = item.CourseId,
                        CourseCode = item.CourseCode,
                        MaterialId = item.MaterialId,
                        MaterialName = item.MaterialName,
                        QuestionText = item.QuestionText,
                        Difficulty = item.Difficulty.ToBusiness(),
                        BloomLevel = item.BloomLevel.ToBusiness(),
                        QuestionType = item.QuestionType.ToBusiness(),
                        Status = item.Status.ToBusiness(),
                        Source = item.Source.ToBusiness(),
                        AuthorName = item.AuthorName,
                        CreatedAt = item.CreatedAt
                    })
                    .ToList();
            },
            "Không thể tải danh sách câu hỏi chờ duyệt.");
    }

    public Task<ServiceResponse> ApproveAsync(
        int questionId,
        int reviewerId,
        int? lecturerId,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            questionId,
            reviewerId,
            lecturerId,
            AssignmentPRN.DataAccess.Enums.QuestionStatus.Approved,
            "duyệt",
            cancellationToken);

    public Task<ServiceResponse> RejectAsync(
        int questionId,
        int reviewerId,
        int? lecturerId,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            questionId,
            reviewerId,
            lecturerId,
            AssignmentPRN.DataAccess.Enums.QuestionStatus.Rejected,
            "từ chối",
            cancellationToken);

    private Task<ServiceResponse> DecideAsync(
        int questionId,
        int reviewerId,
        int? lecturerId,
        AssignmentPRN.DataAccess.Enums.QuestionStatus decision,
        string action,
        CancellationToken cancellationToken)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var id = BusinessValidation.PositiveId(questionId, "câu hỏi");
                var detail = await questionRepository.GetDetailAsync(id, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

                await EnsureCourseVisibleAsync(detail.CourseId, lecturerId, cancellationToken);

                // Only a question still waiting can be decided; an already approved,
                // rejected or archived one is settled, so the lecturer must archive it
                // and add a new one instead of flipping it back and forth.
                if (!IsDecidable(detail.Status.ToBusiness()))
                {
                    throw new BusinessValidationException(
                        $"Câu hỏi đang ở trạng thái {DescribeStatus(detail.Status.ToBusiness())}, "
                        + $"không thể {action} nữa.");
                }

                // An AI question is never dealt before it is approved, so a pending one
                // cannot already sit in a paper; the guard is kept for safety.
                if (await questionRepository.IsAssignedToExamAsync(id, cancellationToken))
                {
                    throw new BusinessValidationException(
                        $"Câu hỏi đã được phát cho lượt thi, không thể {action}.");
                }

                await questionRepository.SetReviewStatusAsync(
                    id,
                    decision,
                    reviewerId,
                    DateTime.Now,
                    cancellationToken);
            },
            "Không thể lưu quyết định duyệt câu hỏi.");
    }

    /// <summary>
    /// True while a question still waits for a review decision. A question already
    /// approved, rejected or archived has been settled.
    /// </summary>
    internal static bool IsDecidable(QuestionStatus status) =>
        status is QuestionStatus.Draft or QuestionStatus.PendingReview;

    private static string DescribeStatus(QuestionStatus status) => status switch
    {
        QuestionStatus.Draft => "nháp",
        QuestionStatus.PendingReview => "chờ duyệt",
        QuestionStatus.Approved => "đã duyệt",
        QuestionStatus.Rejected => "bị từ chối",
        QuestionStatus.Archived => "đã lưu trữ",
        _ => status.ToString()
    };

    /// <summary>Course ids the caller may act on: all of them for an admin, their own for a lecturer.</summary>
    private async Task<IReadOnlyList<int>> VisibleCourseIdsAsync(
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        var courses = await catalogRepository.ListActiveCoursesAsync(cancellationToken);
        return courses
            .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
            .Select(course => course.CourseId)
            .ToList();
    }

    private async Task EnsureCourseVisibleAsync(
        int courseId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue)
        {
            return;
        }

        var allowed = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
        if (!allowed.Contains(courseId))
        {
            throw new BusinessValidationException("Bạn không có quyền thao tác câu hỏi của môn học này.");
        }
    }
}
