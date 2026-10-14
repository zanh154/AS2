using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using AssignmentPRN.Presentation.ViewModels;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin)]
public class AdminController(IExamSessionService examSessionService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await DashboardBuilder.BuildStaffAsync(examSessionService, cancellationToken));
    }
}
