using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamRoom;

/// <summary>
/// Where a student sits their own slot. The paper is dealt the moment they open it, so a
/// session never depends on the lecturer remembering to hand questions out beforehand.
/// </summary>
[SessionAuthorize(RoleNames.Student)]
public abstract class ExamRoomPage(IQuestionService questionService, IVoiceRoomService voiceRoom) : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public object Data { get; private set; } = null!;
    protected IActionResult View(object model) { Data = model; return Page(); }
    protected IActionResult Go(string page, object? values = null) => RedirectToPage("/ExamRoom/" + page, values);
    private IActionResult BackToSchedule() => RedirectToPage("/StudentSchedule/Mine");

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    private async Task<bool> IsVoiceAsync(int id, CancellationToken ct) =>
        await voiceRoom.IsVoiceAsync(id, CurrentUserId, ct);

    protected async Task<IActionResult> SaveDraft(int id, Dictionary<int, int?> answers, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, error = "Đáp án không hợp lệ." });
        var result = await questionService.SaveDraftAsync(id, CurrentUserId, answers ?? [], ct);
        return result.Success ? new JsonResult(new { success = true })
            : BadRequest(new { success = false, error = result.Error });
    }

    /// <summary>
    /// Opens the slot. This deals the paper and starts the clock, so it is a POST: a
    /// crawler or a refresh must not be able to trigger it.
    /// </summary>
    protected async Task<IActionResult> Enter(int id, CancellationToken ct)
    {
        if (await IsVoiceAsync(id, ct)) return RedirectToPage("/VoiceRoom/Index", new { id });
        var result = await questionService.EnterExamAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return BackToSchedule();
        }

        // An oral question has no multiple-choice options. Keep these papers in the
        // voice/text viva room rather than showing an unanswerable radio form.
        if (result.Data.Questions.Any(x => x.Options.Count == 0))
        {
            var entered = await voiceRoom.EnterAsync(id, CurrentUserId, ct);
            if (!entered.Success) { TempData["Error"] = entered.Error; return BackToSchedule(); }
            return RedirectToPage("/VoiceRoom/Index", new { id });
        }

        // Redirect after the write, so refreshing the room is a plain read.
        return Go("Index", new { id });
    }

    protected async Task<IActionResult> Room(int id, CancellationToken ct)
    {
        if (await IsVoiceAsync(id, ct)) return RedirectToPage("/VoiceRoom/Index", new { id });
        var result = await questionService.GetExamRoomAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return BackToSchedule();
        }

        // Reaching the room without a paper means the slot was never opened; send the
        // student back rather than showing an empty exam.
        if (result.Data.Questions.Count == 0)
        {
            TempData["Error"] = "Bạn chưa vào ca thi này. Hãy bấm \"Vào thi\" ở lịch thi.";
            return BackToSchedule();
        }

        if (result.Data.CandidateStatus == CandidateStatus.Completed)
        {
            return Go(nameof(Result), new { id });
        }

        return View(new ExamRoomViewModel { Room = result.Data });
    }

    protected async Task<IActionResult> Result(int id, CancellationToken ct)
    {
        if (await IsVoiceAsync(id, ct)) return RedirectToPage("/VoiceRoom/Index", new { id });
        var result = await questionService.GetExamResultAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return BackToSchedule();
        }

        return View(new ExamResultViewModel { Result = result.Data });
    }

    /// <summary>
    /// Hands in the round being sat. The form posts one radio per question, named by the
    /// slot id, so an unanswered question simply does not appear among the posted values.
    /// </summary>
    protected async Task<IActionResult> Submit(
        int id,
        Dictionary<int, int?> answers,
        CancellationToken ct)
    {
        var result = await questionService.SubmitExamAsync(
            id,
            CurrentUserId,
            answers ?? new Dictionary<int, int?>(),
            ct);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return Go("Index", new { id });
        }

        var room = result.Data;

        // Still open after a submit means the main round went in and follow-ups were dealt.
        if (room.CandidateStatus != CandidateStatus.Completed)
        {
            TempData["Success"] =
                $"Đã nộp vòng câu hỏi chính. Bạn có thêm {room.OpenRound.Count} câu hỏi đào sâu, "
                + "hãy trả lời trong thời gian còn lại.";
            return Go("Index", new { id });
        }

        TempData["Success"] = $"Đã nộp bài: {room.AnsweredCount}/{room.Questions.Count} câu có đáp án.";
        return Go(nameof(Result), new { id });
    }
}
