using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// Entry point of the site. Sends the visitor to their role dashboard when signed in,
/// otherwise to the login page.
/// </summary>
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (!HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        return HttpContext.Session.GetString(SessionKeys.Role) switch
        {
            RoleNames.Admin => RedirectToAction("Index", "Admin"),
            RoleNames.Lecturer => RedirectToAction("Index", "Lecturer"),
            RoleNames.Student => RedirectToAction("Index", "Student"),
            _ => RedirectToPage("/Account/AccessDenied")
        };
    }
}
