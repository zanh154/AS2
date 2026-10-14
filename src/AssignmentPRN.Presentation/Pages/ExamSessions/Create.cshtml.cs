using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
public sealed class CreateModel(IExamSessionService sessions, IQuestionService questions, ICourseMaterialService materials, IExamRealtimeNotifier updates) : ExamSessionsPage(sessions, questions, materials, updates)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => Create(ct);
    public Task<IActionResult> OnPostAsync(ExamSessionCreateViewModel model, CancellationToken ct) => Create(model, ct);
    public Task<IActionResult> OnGetClassRosterAsync(int classId, CancellationToken ct) => ClassRoster(classId, ct);
}
