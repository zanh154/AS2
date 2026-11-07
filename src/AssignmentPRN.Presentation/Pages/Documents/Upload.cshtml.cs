using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents;

/// <summary>
/// Files are uploaded once, as course materials, and indexed for AI from there. This
/// address is kept so old links and bookmarks still land on the right form.
/// </summary>
[StaffPage]
public class UploadModel : PageModel
{
    public IActionResult OnGet(int? courseId) => RedirectToPage("/CourseMaterials/Upload", new { courseId });
}
