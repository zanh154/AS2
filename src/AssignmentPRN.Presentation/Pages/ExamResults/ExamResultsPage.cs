using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

/// <summary>
/// The examiner's view of a session's results. Whether the caller may see a session is
/// decided by <see cref="IExamResultService"/>; these pages only route, exactly as the
/// controller they replace did.
/// </summary>
[StaffPage]
public abstract class ExamResultsPage(IExamResultService resultService) : PageModel
{
    /// <summary>The model the page hands to its partial.</summary>
    public object Data { get; private set; } = null!;

    /// <summary>Null for an admin, who sees every session.</summary>
    protected int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    protected IExamResultService Results { get; } = resultService;

    protected IActionResult View(object model)
    {
        Data = model;
        return Page();
    }

    protected async Task<IActionResult> Session(int id, CancellationToken ct)
    {
        var result = await Results.GetSessionResultsAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage("/ExamSessions/Index");
        }

        return View(new SessionResultViewModel { Session = result.Data });
    }

    protected async Task<IActionResult> Paper(int id, CancellationToken ct)
    {
        var result = await Results.GetCandidateResultAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage("/ExamSessions/Index");
        }

        return View(new CandidateResultViewModel { Candidate = result.Data });
    }
}
