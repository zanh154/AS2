using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.VoiceRoom;

[RequestSizeLimit(13 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 13 * 1024 * 1024)]
public sealed class IndexModel(IVoiceRoomService service, IHttpClientFactory? httpClientFactory = null) : PageModel
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
        if (UserId <= 0) return RedirectToPage("/Account/Login");
        if (!IsStudent) return RedirectToPage("/Account/AccessDenied");
        var result = await service.EnterAsync(id, UserId, ct);
        if (result.Success) return RedirectToPage(new { id });
        TempData["Error"] = result.Error;
        return RedirectToPage("/StudentSchedule/Mine");
    }

    public sealed record ProctorViolationPayload(int Count, string? Reason);

    public async Task<IActionResult> OnPostProctorViolationAsync(int id, [FromBody] ProctorViolationPayload? payload, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        if (payload is null || payload.Count <= 0) return BadRequest();
        var result = await service.RecordProctorViolationAsync(id, UserId, payload.Count, payload.Reason, ct);
        return result.Success ? new JsonResult(result.Data) : BadRequest(new { error = result.Error });
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

    /// <summary>
    /// Provides crisp Vietnamese/English TTS audio when the student's browser/OS lacks
    /// a matching local voice (e.g., Windows Chrome without Vietnamese SAPI5 voices).
    /// </summary>
    public async Task<IActionResult> OnGetTtsAsync(int id, string? text, string? lang, CancellationToken ct)
    {
        if (!IsStudent) return StatusCode(403);
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return BadRequest();
        if (trimmed.Length > 1500) trimmed = trimmed[..1500];

        var tl = lang?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "vi";
        var chunks = SplitTtsChunks(trimmed, 180);
        var client = httpClientFactory?.CreateClient() ?? new HttpClient();
        using var buffer = new MemoryStream();
        foreach (var chunk in chunks)
        {
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(chunk)}&tl={tl}&client=tw-ob";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0");
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return StatusCode(502);
            await resp.Content.CopyToAsync(buffer, ct);
        }

        Response.Headers.CacheControl = "private, no-store";
        return File(buffer.ToArray(), "audio/mpeg");
    }

    private static readonly char[] TtsSplitChars = ['.', '?', '!', ',', ';'];

    private static List<string> SplitTtsChunks(string text, int maxLen)
    {
        var chunks = new List<string>();
        var remaining = text.AsSpan().Trim();
        while (remaining.Length > maxLen)
        {
            var slice = remaining[..maxLen];
            var splitAt = slice.LastIndexOfAny(TtsSplitChars);
            if (splitAt < maxLen / 3) splitAt = slice.LastIndexOf(' ');
            if (splitAt <= 0) splitAt = maxLen;
            chunks.Add(remaining[..splitAt].Trim().ToString());
            remaining = remaining[splitAt..].TrimStart(" ,");
        }
        if (remaining.Length > 0) chunks.Add(remaining.ToString());
        return chunks;
    }
}
