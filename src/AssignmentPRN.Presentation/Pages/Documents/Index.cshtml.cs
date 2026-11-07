using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents;

/// <summary>
/// The document list merged into the course material list, which now shows each file's
/// AI status. This address is kept so old links and bookmarks still arrive there.
/// </summary>
[StaffPage]
public class IndexModel : PageModel
{
    public IActionResult OnGet(int? courseId) =>
        RedirectToPage("/CourseMaterials/Index", new { courseId = courseId > 0 ? courseId : null });
}
