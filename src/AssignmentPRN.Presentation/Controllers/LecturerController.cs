using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Lecturer)]
public class LecturerController(IExamSessionService examSessionService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var lecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        return View(await DashboardBuilder.BuildStaffAsync(examSessionService, cancellationToken, lecturerId));
    }
}
