using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.VoiceRoom;

[RequestSizeLimit(13 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 13 * 1024 * 1024)]
public sealed class IndexModel(IVoiceRoomService service) : PageModel
{
    private int UserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    public int RecoveryUserId => UserId;
    private bool IsStudent => UserId > 0 && HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Student;
    public int CandidateId { get; private set; }
    public IActionResult OnGet(int id)
    {
        Response.Headers.CacheControl = "private, no-store";
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
        return RedirectToPage("/StudentSchedule/Mine");
    }

    public async Task<IActionResult> OnPostSyncAsync(int id, [FromBody] VoiceAnswerRequest? answer, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!IsStudent) return StatusCode(403);
        if (!ModelState.IsValid) return BadRequest();
        var result = await service.SyncAsync(id, UserId, answer, ct);
        return result.Success ? new JsonResult(result.Data) : BadRequest(new { error = result.Error });
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, int questionId, double durationSeconds, IFormFile? audio, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        if (!ModelState.IsValid || audio == null || audio.Length <= 0 || audio.Length > 12 * 1024 * 1024) return BadRequest(new { error = "Bản thu không hợp lệ hoặc vượt 12 MB." });
        await using var stream = audio.OpenReadStream();
        var result = await service.UploadAsync(id, UserId, questionId, audio.ContentType, audio.Length, durationSeconds, stream, ct);
        return result.Success ? new JsonResult(result.Data) : BadRequest(new { error = result.Error });
    }

    public async Task<IActionResult> OnGetAudioAsync(int id, int questionId, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        var result = await service.DownloadAsync(id, UserId, questionId, ct);
        if (!result.Success || result.Data == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Data.Content, result.Data.ContentType);
    }
}
