using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Questions;

/// <summary>
/// The review desk for AI-generated questions. Only approved questions reach the dealing
/// pool, so an AI question waits here until a lecturer of its course decides. The page is a
/// thin shell over <see cref="IQuestionReviewService"/>, which owns the course-visibility
/// rule; hiding a button never grants access.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ReviewModel(
    IQuestionReviewService reviewService,
    ICatalogService catalog) : PageModel
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    public IReadOnlyList<QuestionListItemResponse> Questions { get; private set; } = [];

    public IReadOnlyList<SelectListItem> CourseOptions { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    public string? LoadError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, CancellationToken ct) =>
        await DecideAsync(id, approve: true, ct);

    public async Task<IActionResult> OnPostRejectAsync(int id, CancellationToken ct) =>
        await DecideAsync(id, approve: false, ct);

    private async Task<IActionResult> DecideAsync(int id, bool approve, CancellationToken ct)
    {
        var result = approve
            ? await reviewService.ApproveAsync(id, CurrentUserId, LecturerId, ct)
            : await reviewService.RejectAsync(id, CurrentUserId, LecturerId, ct);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? approve
                ? "Đã duyệt câu hỏi, câu này đã sẵn sàng để phát đề."
                : "Đã từ chối câu hỏi, câu này sẽ không được phát."
            : result.Error;

        return RedirectToPage(new { courseId = CourseId });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var courses = await catalog.ListActiveCoursesAsync(ct);
        var allowed = (courses.Data ?? [])
            .Where(course => !LecturerId.HasValue || course.LecturerId == LecturerId)
            .OrderBy(course => course.CourseCode)
            .ToList();

        CourseOptions = allowed
            .Select(course => new SelectListItem(
                $"{course.CourseCode} · {course.CourseName}",
                course.CourseId.ToString()))
            .ToList();

        // A course filter the caller may not see must not fall through to the service.
        CourseId = CourseId is int requested && allowed.Any(course => course.CourseId == requested)
            ? requested
            : null;

        var result = await reviewService.ListPendingAsync(LecturerId, CourseId, ct);
        Questions = result.Data ?? [];
        LoadError = result.Success ? null : result.Error;
    }
}
