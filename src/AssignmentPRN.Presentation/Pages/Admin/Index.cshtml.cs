using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Admin;

[SessionAuthorize(RoleNames.Admin)]
public sealed class IndexModel(IExamSessionService examSessionService) : PageModel
{
    public StaffDashboardViewModel Dashboard { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await DashboardBuilder.BuildStaffAsync(examSessionService, cancellationToken);
    }
}
