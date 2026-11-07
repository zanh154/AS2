using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
public sealed class EditModel(IExamSessionService sessions, IQuestionService questions, ICourseMaterialService materials, IExamRealtimeNotifier updates) : ExamSessionsPage(sessions, questions, materials, updates)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Edit(id, ct);
    public Task<IActionResult> OnPostAsync(int id, ExamSessionEditViewModel model, CancellationToken ct) => Edit(id, model, ct);
}
