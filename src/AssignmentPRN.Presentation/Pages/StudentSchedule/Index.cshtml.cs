using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.StudentSchedule;

/// <summary>
/// Staff lookup. Searching by name or email beats a dropdown once the
/// student list grows past a handful of rows.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public sealed class IndexModel(IExamSessionService examSessionService) : PageModel
{
    public StudentScheduleViewModel Schedule { get; private set; } = null!;

    public async Task OnGetAsync(string? q, int? studentId, CancellationToken cancellationToken)
    {
        var model = new StudentScheduleViewModel { Query = q?.Trim() ?? string.Empty };
        Schedule = model;

        // Picking a name from the result list jumps straight to that schedule.
        if (studentId is > 0)
        {
            model.HasSearched = true;
            var picked = await examSessionService.GetStudentScheduleAsync(studentId.Value, cancellationToken);
            model.Schedule = picked.Data;
            model.Error = picked.Success ? null : picked.Error;
            return;
        }

        if (string.IsNullOrWhiteSpace(model.Query))
        {
            return;
        }

        model.HasSearched = true;
        var search = await examSessionService.SearchStudentsAsync(model.Query, cancellationToken);
        if (!search.Success || search.Data is null)
        {
            model.Error = search.Error;
            return;
        }

        // One hit is unambiguous, so skip the extra click.
        if (search.Data.Count == 1)
        {
            var only = await examSessionService.GetStudentScheduleAsync(search.Data[0].UserId, cancellationToken);
            model.Schedule = only.Data;
            model.Error = only.Success ? null : only.Error;
            return;
        }

        model.Matches = search.Data;
    }
}
