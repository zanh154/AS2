using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Rubrics;

/// <summary>
/// Lists rubrics with course filter and search.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class IndexModel(
    IRubricService rubricService,
    ICatalogService catalog) : PageModel
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    public IReadOnlyList<RubricListItemResponse> Rubrics { get; private set; } = [];

    public IReadOnlyList<SelectListItem> CourseOptions { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Term { get; set; }

    public string? LoadError { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int? courseId, string? term, CancellationToken ct)
    {
        var result = await rubricService.DeleteAsync(id, LecturerId, ct);
        if (result.Success)
        {
            TempData["Success"] = "Đã xoá bộ tiêu chí.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Không thể xoá bộ tiêu chí.";
        }

        return RedirectToPage("Index", new { courseId, term });
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

        CourseId = CourseId is int requested && allowed.Any(course => course.CourseId == requested)
            ? requested
            : null;

        var result = await rubricService.ListAsync(LecturerId, CourseId, Term, ct);
        Rubrics = result.Data ?? [];
        LoadError = result.Success ? null : result.Error;
    }
}