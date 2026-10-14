using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// The examiner's view of a session's results. Whether the caller may see a session is
/// decided by <see cref="IExamResultService"/>; this controller only routes.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ExamResultsController(IExamResultService resultService) : Controller
{
    /// <summary>Null for an admin, who sees every session.</summary>
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken ct)
    {
        var result = await resultService.GetSessionResultsAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage("/ExamSessions/Index");
        }

        return View(new SessionResultViewModel { Session = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Candidate(int id, CancellationToken ct)
    {
        var result = await resultService.GetCandidateResultAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage("/ExamSessions/Index");
        }

        return View(new CandidateResultViewModel { Candidate = result.Data });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseOverdue(int id, CancellationToken ct)
    {
        var result = await resultService.CloseOverdueSlotsAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? $"Đã chốt ca thi: {result.Data!.MarkedAbsent} sinh viên vắng, "
              + $"{result.Data.Completed} bài chưa nộp được đóng lại."
            : result.Error;

        return RedirectToAction(nameof(Index), new { id });
    }
}
