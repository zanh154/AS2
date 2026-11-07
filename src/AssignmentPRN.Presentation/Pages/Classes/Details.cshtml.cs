using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AssignmentPRN.Presentation.Pages.Classes;
/// <summary>Old address of the roster screen, kept so existing links still land somewhere.</summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public sealed class DetailsModel : PageModel
{
    public IActionResult OnGet(int id) => RedirectToPage("/Classes/Roster", new { id });
}
