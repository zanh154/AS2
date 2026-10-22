using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamRoom;
public sealed class IndexModel(IQuestionService questions, IVoiceRoomService voiceRoom) : ExamRoomPage(questions, voiceRoom)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Room(id, ct);
    public Task<IActionResult> OnPostSubmitAsync(int id, [FromForm] Dictionary<int, int?> answers, CancellationToken ct) => Submit(id, answers, ct);
    public Task<IActionResult> OnPostSaveDraftAsync(int id, [FromForm] Dictionary<int, int?> answers, CancellationToken ct) => SaveDraft(id, answers, ct);
}
