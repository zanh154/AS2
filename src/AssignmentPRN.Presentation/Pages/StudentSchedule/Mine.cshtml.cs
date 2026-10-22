using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.StudentSchedule;

/// <summary>The signed-in student's own exam schedule.</summary>
[SessionAuthorize(RoleNames.Student)]
public sealed class MineModel(IExamSessionService examSessionService) : PageModel
{
    public StudentScheduleViewModel Schedule { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        var response = await examSessionService.GetStudentScheduleAsync(userId.Value, cancellationToken);

        Schedule = new StudentScheduleViewModel
        {
            IsOwnSchedule = true,
            Schedule = response.Data,
            Error = response.Success ? null : response.Error
        };
        return Page();
    }
}
