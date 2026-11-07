using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamRoom;
public sealed class ResultModel(IQuestionService questions, IVoiceRoomService voiceRoom) : ExamRoomPage(questions, voiceRoom)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Result(id, ct);
}
