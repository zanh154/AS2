using AssignmentPRN.Business.Services;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.MyResults;

/// <summary>
/// A student's own report after a viva: before the lecturer publishes the final grade,
/// only the student's submitted answers and follow-up replies are shown and all scores
/// are withheld. Once finalized by the lecturer, the published score and feedback appear.
/// </summary>
public sealed class IndexModel(
    IResultReviewService reviewService,
    IGradingService? gradingService = null) : PageModel
{
    public CandidateReview Review { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        _ = gradingService;
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

        Review = result.Data;
        return Page();
    }
}
