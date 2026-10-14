using AssignmentPRN.Business.Interfaces;
using DaContracts = AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// The rubric editor: creates and maintains grading criteria for questions.
/// A lecturer only sees courses they teach; the check lives here rather than in the page.
/// </summary>
public class RubricService(
    IQuestionRepository questionRepository,
    ICatalogRepository catalogRepository) : IRubricService
{
    public Task<ServiceResponse<IReadOnlyList<RubricListItemResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        string? term = null,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<RubricListItemResponse>>(
            async () =>
            {
                var allowedCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (allowedCourseIds.Count == 0)
                {
                    return (IReadOnlyList<RubricListItemResponse>)Array.Empty<RubricListItemResponse>();
                }

                if (courseId is int requested && !allowedCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem bộ tiêu chí của môn học này.");
                }

                var effectiveCourseId = courseId ?? (allowedCourseIds.Count == 1 ? allowedCourseIds[0] : null);

                var items = await questionRepository.ListRubricsAsync(effectiveCourseId, term, cancellationToken);

                return items
                    .Where(item => allowedCourseIds.Contains(item.CourseId))
                    .Select(item => new RubricListItemResponse
                    {
                        RubricId = item.RubricId,
                        QuestionId = item.QuestionId,
                        QuestionText = item.QuestionText,
                        CourseCode = item.CourseCode,
                        CourseName = item.CourseName,
                        TotalScore = item.TotalScore,
                        CriteriaCount = item.CriteriaCount,
                        CreatedAt = item.CreatedAt,
                        UpdatedAt = item.UpdatedAt
                    })
                    .ToList();
            },
            "Không thể tải danh sách bộ tiêu chí.");
    }

    public Task<ServiceResponse<RubricDetailResponse>> GetAsync(
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<RubricDetailResponse>(
            async () =>
            {
                var detail = await questionRepository.GetRubricAsync(questionId, cancellationToken);
                if (detail is null)
                {
                    throw new BusinessValidationException("Không tìm thấy bộ tiêu chí cho câu hỏi này.");
                }

                await EnsureCourseVisibleAsync(questionId, lecturerId, cancellationToken);

                return MapDetail(detail);
            },
            "Không thể tải bộ tiêu chí.");
    }

    public Task<ServiceResponse<RubricDetailResponse>> SaveAsync(
        RubricSaveRequest request,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<RubricDetailResponse>(
            async () =>
            {
                // Validate criteria
                if (request.Criteria.Count == 0)
                {
                    throw new BusinessValidationException("Bộ tiêu chí phải có ít nhất một tiêu chí.");
                }

                foreach (var criterion in request.Criteria)
                {
                    if (string.IsNullOrWhiteSpace(criterion.CriteriaName))
                    {
                        throw new BusinessValidationException("Tên tiêu chí không được để trống.");
                    }
                    if (criterion.MaxScore <= 0)
                    {
                        throw new BusinessValidationException($"Điểm tối đa của tiêu chí '{criterion.CriteriaName}' phải lớn hơn 0.");
                    }
                }

                // Check for duplicate display orders
                var duplicateOrders = request.Criteria
                    .GroupBy(c => c.DisplayOrder)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicateOrders.Count > 0)
                {
                    throw new BusinessValidationException($"Thứ tự hiển thị bị trùng: {string.Join(", ", duplicateOrders)}.");
                }

                // Ensure question is visible
                await EnsureCourseVisibleAsync(request.QuestionId, lecturerId, cancellationToken);

                // Map to DataAccess input
                var input = new DaContracts.RubricUpsertInput
                {
                    RubricId = request.RubricId,
                    QuestionId = request.QuestionId,
                    Criteria = request.Criteria
                        .Select(c => new DaContracts.RubricCriteriaInput
                        {
                            CriteriaId = c.CriteriaId,
                            CriteriaName = c.CriteriaName,
                            Description = c.Description,
                            MaxScore = c.MaxScore,
                            DisplayOrder = c.DisplayOrder
                        })
                        .ToList()
                };

                var detail = await questionRepository.SaveRubricAsync(input, cancellationToken);
                return MapDetail(detail);
            },
            "Không thể lưu bộ tiêu chí.");
    }

    public Task<ServiceResponse> DeleteAsync(
        int rubricId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var questionAndCourse = await questionRepository.GetRubricQuestionAndCourseAsync(rubricId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy bộ tiêu chí.");

                await EnsureCourseVisibleAsync(questionAndCourse.QuestionId, lecturerId, cancellationToken);

                await questionRepository.DeleteRubricAsync(rubricId, cancellationToken);
            },
            "Không thể xoá bộ tiêu chí.");
    }

    private RubricDetailResponse MapDetail(DaContracts.RubricDetail detail) => new()
    {
        RubricId = detail.RubricId,
        QuestionId = detail.QuestionId,
        QuestionText = detail.QuestionText,
        TotalScore = detail.TotalScore,
        CreatedAt = detail.CreatedAt,
        UpdatedAt = detail.UpdatedAt,
        Criteria = detail.Criteria
            .Select(c => new RubricCriteriaResponse
            {
                CriteriaId = c.CriteriaId,
                CriteriaName = c.CriteriaName,
                Description = c.Description,
                MaxScore = c.MaxScore,
                DisplayOrder = c.DisplayOrder
            })
            .ToList()
    };

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
        int questionId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue)
        {
            return;
        }

        var detail = await questionRepository.GetDetailAsync(questionId, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

        var allowedCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
        if (!allowedCourseIds.Contains(detail.CourseId))
        {
            throw new BusinessValidationException("Bạn không có quyền thao tác bộ tiêu chí của môn học này.");
        }
    }

    /// <summary>Validates rubric criteria for the SaveAsync method. Exposed as static for unit testing.</summary>
    internal static void ValidateCriteria(IReadOnlyList<RubricCriteriaInput> criteria)
    {
        if (criteria.Count == 0)
        {
            throw new BusinessValidationException("Bộ tiêu chí phải có ít nhất một tiêu chí.");
        }

        foreach (var criterion in criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.CriteriaName))
            {
                throw new BusinessValidationException("Tên tiêu chí không được để trống.");
            }
            if (criterion.MaxScore <= 0)
            {
                throw new BusinessValidationException($"Điểm tối đa của tiêu chí '{criterion.CriteriaName}' phải lớn hơn 0.");
            }
        }

        var duplicateOrders = criteria
            .GroupBy(c => c.DisplayOrder)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateOrders.Count > 0)
        {
            throw new BusinessValidationException($"Thứ tự hiển thị bị trùng: {string.Join(", ", duplicateOrders)}.");
        }
    }
}