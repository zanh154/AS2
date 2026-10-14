using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class UploadModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ICatalogRepository _catalogRepository;

    public UploadModel(IDocumentService documentService, ICatalogRepository catalogRepository)
    {
        _documentService = documentService;
        _catalogRepository = catalogRepository;
    }

    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int MaxFileSizeMB { get; set; } = 50;

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int courseId, CancellationToken cancellationToken)
    {
        CourseId = courseId;

        // Verify course exists
        var course = await _catalogRepository.ListActiveCoursesAsync(cancellationToken);
        var found = course.FirstOrDefault(c => c.CourseId == courseId);
        if (found is null)
        {
            return NotFound();
        }

        CourseName = $"{found.CourseCode} - {found.CourseName}";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        CourseId = Input.CourseId;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        var response = await _documentService.UploadAsync(CourseId, userId, Input.File!, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = "Đã tải lên tài liệu. Hệ thống đang xử lý trong nền.";
            return RedirectToPage("Index", new { courseId = CourseId });
        }

        Error = response.Error;
        return Page();
    }

    public sealed class UploadInput
    {
        public int CourseId { get; set; }
        public IFormFile? File { get; set; }
    }
}