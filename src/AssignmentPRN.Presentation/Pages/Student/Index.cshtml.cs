using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Student;

[SessionAuthorize(RoleNames.Student)]
public sealed class IndexModel(IExamSessionService examSessionService) : PageModel
{
    public StudentDashboardViewModel Dashboard { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        Dashboard = await DashboardBuilder.BuildStudentAsync(examSessionService, userId.Value, cancellationToken);
        return Page();
    }
}
