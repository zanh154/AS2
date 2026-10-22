using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Lecturer;

[SessionAuthorize(RoleNames.Lecturer)]
public sealed class IndexModel(IExamSessionService examSessionService) : PageModel
{
    public StaffDashboardViewModel Dashboard { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await DashboardBuilder.BuildStaffAsync(examSessionService, cancellationToken,
            HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0);
    }
}
