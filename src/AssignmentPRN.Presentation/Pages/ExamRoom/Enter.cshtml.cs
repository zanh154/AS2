using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamRoom;
public sealed class EnterModel(IQuestionService questions, IVoiceRoomService voiceRoom) : ExamRoomPage(questions, voiceRoom)
{
    /// <summary>Entering deals the paper, so a plain visit only goes back to the schedule.</summary>
    public IActionResult OnGet() => RedirectToPage("/StudentSchedule/Mine");
    public Task<IActionResult> OnPostAsync(int id, CancellationToken ct) => Enter(id, ct);
}
