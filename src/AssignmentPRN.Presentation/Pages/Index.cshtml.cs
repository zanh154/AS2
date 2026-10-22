using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages;

/// <summary>
/// Entry point of the site. Sends the visitor to their role dashboard when signed in,
/// otherwise to the login page.
/// </summary>
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (!HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        return HttpContext.Session.GetString(SessionKeys.Role) switch
        {
            RoleNames.Admin => RedirectToPage("/Admin/Index"),
            RoleNames.Lecturer => RedirectToPage("/Lecturer/Index"),
            RoleNames.Student => RedirectToPage("/Student/Index"),
            _ => RedirectToPage("/Account/AccessDenied")
        };
    }
}
