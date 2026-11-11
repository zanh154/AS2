using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.StudentSchedule;

/// <summary>The signed-in student's own exam schedule.</summary>
[SessionAuthorize(RoleNames.Student)]
public sealed class MineModel(IExamSessionService examSessionService, IVoiceRoomService? voiceRoomService = null) : PageModel
{
    public StudentScheduleViewModel Schedule { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        var response = await examSessionService.GetStudentScheduleAsync(userId.Value, cancellationToken);

        Schedule = new StudentScheduleViewModel
        {
            IsOwnSchedule = true,
            Schedule = response.Data,
            Error = response.Success ? null : response.Error
        };
        return Page();
    }

    public async Task<IActionResult> OnPostContinueAsync(int examId, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (!userId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        var continued = await examSessionService.ContinueAsStudentAsync(examId, userId.Value, cancellationToken);
        if (!continued.Success || continued.Data is null)
        {
            TempData["Error"] = continued.Error ?? "Không thể mở lượt thi tiếp.";
            return RedirectToPage();
        }

        var nextSlot = continued.Data.Candidates
            .Where(c => c.StudentId == userId.Value && c.Status == CandidateStatus.Waiting)
            .OrderByDescending(c => c.CandidateId)
            .FirstOrDefault();

        if (nextSlot is not null && voiceRoomService is not null)
        {
            var entered = await voiceRoomService.EnterAsync(nextSlot.CandidateId, userId.Value, cancellationToken);
            if (entered.Success)
            {
                return RedirectToPage("/VoiceRoom/Index", new { id = nextSlot.CandidateId });
            }

            TempData["Error"] = entered.Error;
            return RedirectToPage();
        }

        TempData["Success"] = "Đã tạo lượt thi tiếp mới cho bạn.";
        return RedirectToPage();
    }
}
