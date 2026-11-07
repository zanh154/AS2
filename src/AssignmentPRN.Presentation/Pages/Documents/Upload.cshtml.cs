using System.ComponentModel.DataAnnotations;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Presentation.Pages.Documents;

[StaffPage]
public class UploadModel(IDocumentService documents, ICatalogService catalog, IOptions<DocumentStorageOptions> storage) : PageModel
{
    public int CourseId { get; private set; }
    public string CourseName { get; private set; } = string.Empty;
    public int MaxFileSizeMB => storage.Value.MaxFileSizeMB;
    [BindProperty] public UploadInput Input { get; set; } = new();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(int courseId, CancellationToken ct)
    {
        Input.CourseId = courseId;
        return await LoadCourseAsync(courseId, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!await LoadCourseAsync(Input.CourseId, ct)) return NotFound();
        if (!ModelState.IsValid) return Page();
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        var response = await documents.UploadAsync(Input.CourseId, userId, Input.File!, ct);
        if (!response.Success) { Error = response.Error; return Page(); }
        TempData["Success"] = "Đã tải lên tài liệu. Hệ thống đang xử lý trong nền.";
        return RedirectToPage("Index", new { courseId = Input.CourseId });
    }

    private async Task<bool> LoadCourseAsync(int courseId, CancellationToken ct)
    {
        var courses = await catalog.ListActiveCoursesAsync(ct);
        var course = courses.Data?.FirstOrDefault(x => x.CourseId == courseId &&
            (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Admin || x.LecturerId == HttpContext.Session.GetInt32(SessionKeys.UserId)));
        if (course is null) return false;
        CourseId = course.CourseId;
        CourseName = $"{course.CourseCode} - {course.CourseName}";
        return true;
    }

    public sealed class UploadInput
    {
        [Range(1, int.MaxValue)] public int CourseId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn file.")] public IFormFile? File { get; set; }
    }
}
