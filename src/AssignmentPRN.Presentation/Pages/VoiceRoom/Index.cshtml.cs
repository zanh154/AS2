using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.VoiceRoom;

public sealed class IndexModel(IVoiceRoomService service) : PageModel
{
    private int UserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    private bool IsStudent => UserId > 0 && HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Student;
    public int CandidateId { get; private set; }
    public IActionResult OnGet(int id)
    {
        if (!IsStudent) return RedirectToPage("/Account/Login");
        CandidateId = id;
        return Page();
    }

    public async Task<IActionResult> OnPostEnterAsync(int id, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        var result = await service.EnterAsync(id, UserId, ct);
        if (result.Success) return RedirectToPage(new { id });
        TempData["Error"] = result.Error;
        return RedirectToAction("Mine", "StudentSchedule");
    }

    public async Task<IActionResult> OnPostSyncAsync(int id, [FromBody] VoiceAnswerRequest? answer, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        if (!ModelState.IsValid) return BadRequest();
        var result = await service.SyncAsync(id, UserId, answer, ct);
        return result.Success ? new JsonResult(result.Data) : BadRequest(new { error = result.Error });
    }
}
