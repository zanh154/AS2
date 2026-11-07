using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
[StaffPage]
public sealed class SettingsModel(IExamVivaSettingsService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty, Range(15, 1800)] public int SecondsPerQuestion { get; set; }
    [BindProperty, Range(0, 5)] public int MaxFollowUpsPerQuestion { get; set; }
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) {
        var result = await service.GetAsync(Id, LecturerId, ct);
        if (!result.Success || result.Data is null) { TempData["Error"] = result.Error; return RedirectToPage("/ExamSessions/Index"); }
        SecondsPerQuestion = result.Data.SecondsPerQuestion; MaxFollowUpsPerQuestion = result.Data.MaxFollowUpsPerQuestion;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct) {
        if (!ModelState.IsValid) return Page();
        var result = await service.SaveAsync(Id, new(SecondsPerQuestion, MaxFollowUpsPerQuestion), LecturerId, ct);
        if (!result.Success) { ModelState.AddModelError("", result.Error!); return Page(); }
        TempData["Success"] = "Đã lưu cấu hình vấn đáp.";
        return RedirectToPage("/ExamSessions/Details", new { id = Id });
    }
}
