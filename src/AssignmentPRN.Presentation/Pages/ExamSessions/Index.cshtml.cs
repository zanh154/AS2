using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
public sealed class IndexModel(IExamSessionService sessions, IQuestionService questions, ICourseMaterialService materials, IExamRealtimeNotifier updates) : ExamSessionsPage(sessions, questions, materials, updates)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => Index(ct);
    public async Task<IActionResult> OnGetSnapshotAsync(CancellationToken ct) { await Index(ct); return Partial("/Pages/Shared/ExamSessionsIndex.cshtml", Data); }
}
