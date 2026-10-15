using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// Where a student sits their own slot. The paper is dealt the moment they open it, so a
/// session never depends on the lecturer remembering to hand questions out beforehand.
/// </summary>
[SessionAuthorize(RoleNames.Student)]
public class ExamRoomController(IQuestionService questionService) : Controller
{
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int id, [FromForm] Dictionary<int, int?> answers, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, error = "Đáp án không hợp lệ." });
        var result = await questionService.SaveDraftAsync(id, CurrentUserId, answers ?? [], ct);
        return result.Success ? Json(new { success = true })
            : BadRequest(new { success = false, error = result.Error });
    }

    /// <summary>
    /// Opens the slot. This deals the paper and starts the clock, so it is a POST: a
    /// crawler or a refresh must not be able to trigger it.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int id, CancellationToken ct)
    {
        var result = await questionService.EnterExamAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "StudentSchedule");
        }

        // Redirect after the write, so refreshing the room is a plain read.
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken ct)
    {
        var result = await questionService.GetExamRoomAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "StudentSchedule");
        }

        // Reaching the room without a paper means the slot was never opened; send the
        // student back rather than showing an empty exam.
        if (result.Data.Questions.Count == 0)
        {
            TempData["Error"] = "Bạn chưa vào ca thi này. Hãy bấm \"Vào thi\" ở lịch thi.";
            return RedirectToAction("Mine", "StudentSchedule");
        }

        if (result.Data.CandidateStatus == CandidateStatus.Completed)
        {
            return RedirectToAction(nameof(Result), new { id });
        }

        return View(new ExamRoomViewModel { Room = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id, CancellationToken ct)
    {
        var result = await questionService.GetExamResultAsync(id, CurrentUserId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Mine", "StudentSchedule");
        }

        return View(new ExamResultViewModel { Result = result.Data });
    }

    /// <summary>
    /// Hands in the round being sat. The form posts one radio per question, named by the
    /// slot id, so an unanswered question simply does not appear among the posted values.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        int id,
        [FromForm] Dictionary<int, int?> answers,
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
            return RedirectToAction(nameof(Index), new { id });
        }

        var room = result.Data;

        // Still open after a submit means the main round went in and follow-ups were dealt.
        if (room.CandidateStatus != CandidateStatus.Completed)
        {
            TempData["Success"] =
                $"Đã nộp vòng câu hỏi chính. Bạn có thêm {room.OpenRound.Count} câu hỏi đào sâu, "
                + "hãy trả lời trong thời gian còn lại.";
            return RedirectToAction(nameof(Index), new { id });
        }

        TempData["Success"] = $"Đã nộp bài: {room.AnsweredCount}/{room.Questions.Count} câu có đáp án.";
        return RedirectToAction(nameof(Result), new { id });
    }
}
