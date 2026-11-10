using AssignmentPRN.Business.Services;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.MyResults;

/// <summary>
/// A student's own report after a viva: the mark that counts and what the AI said about
/// each answer. The service decides whether the slot is theirs and finished; this page
/// only checks that a student is signed in and routes.
/// </summary>
public sealed class IndexModel(
    IResultReviewService reviewService,
    IGradingService? gradingService = null) : PageModel
{
    public CandidateReview Review { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";

        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        if (userId <= 0) return RedirectToPage("/Account/Login", new { returnUrl = Request.Path.Value });
        if (HttpContext.Session.GetString(SessionKeys.Role) != RoleNames.Student) return StatusCode(403);

        var result = await reviewService.GetForStudentAsync(id, userId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage("/StudentSchedule/Mine");
        }

        if (!result.Data.IsDisqualified
            && gradingService is not null
            && result.Data.Answers.Any(a => a.NotGraded
                && (!string.IsNullOrWhiteSpace(a.Transcript)
                    || a.Probes.Any(p => !string.IsNullOrWhiteSpace(p.Transcript)))))
        {
            await gradingService.GradeCandidateAsync(id, ct);
            result = await reviewService.GetForStudentAsync(id, userId, ct);
            if (!result.Success || result.Data is null)
            {
                TempData["Error"] = result.Error;
                return RedirectToPage("/StudentSchedule/Mine");
            }
        }

        Review = result.Data;
        return Page();
    }
}
