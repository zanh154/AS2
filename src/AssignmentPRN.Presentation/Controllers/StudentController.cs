using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using AssignmentPRN.Presentation.ViewModels;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Student)]
public class StudentController(IExamSessionService examSessionService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        return View(await DashboardBuilder.BuildStudentAsync(examSessionService, userId.Value, cancellationToken));
    }
}
