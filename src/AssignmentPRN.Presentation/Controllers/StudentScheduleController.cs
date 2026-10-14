using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

public class StudentScheduleController(IExamSessionService examSessionService) : Controller
{
    /// <summary>The signed-in student's own exam schedule.</summary>
    [HttpGet]
    [SessionAuthorize(RoleNames.Student)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        var response = await examSessionService.GetStudentScheduleAsync(userId.Value, cancellationToken);

        return View(new StudentScheduleViewModel
        {
            IsOwnSchedule = true,
            Schedule = response.Data,
            Error = response.Success ? null : response.Error
        });
    }

    /// <summary>
    /// Staff lookup. Searching by name or email beats a dropdown once the
    /// student list grows past a handful of rows.
    /// </summary>
    [HttpGet]
    [SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
    public async Task<IActionResult> Index(string? q, int? studentId, CancellationToken cancellationToken)
    {
        var model = new StudentScheduleViewModel { Query = q?.Trim() ?? string.Empty };

        // Picking a name from the result list jumps straight to that schedule.
        if (studentId is > 0)
        {
            model.HasSearched = true;
            var picked = await examSessionService.GetStudentScheduleAsync(studentId.Value, cancellationToken);
            model.Schedule = picked.Data;
            model.Error = picked.Success ? null : picked.Error;
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.Query))
        {
            return View(model);
        }

        model.HasSearched = true;
        var search = await examSessionService.SearchStudentsAsync(model.Query, cancellationToken);
        if (!search.Success || search.Data is null)
        {
            model.Error = search.Error;
            return View(model);
        }

        // One hit is unambiguous, so skip the extra click.
        if (search.Data.Count == 1)
        {
            var only = await examSessionService.GetStudentScheduleAsync(search.Data[0].UserId, cancellationToken);
            model.Schedule = only.Data;
            model.Error = only.Success ? null : only.Error;
            return View(model);
        }

        model.Matches = search.Data;
        return View(model);
    }
}
