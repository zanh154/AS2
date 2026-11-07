using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
public sealed class DetailsModel(IExamSessionService sessions, IQuestionService questions, ICourseMaterialService materials, IExamRealtimeNotifier updates) : ExamSessionsPage(sessions, questions, materials, updates)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Details(id, ct);
    public Task<IActionResult> OnPostChangeStatusAsync(int id, ExamSessionStatus status, CancellationToken ct) => ChangeStatus(id, status, ct);
    public Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) => Delete(id, ct);
    public Task<IActionResult> OnPostAddStudentAsync(ExamSessionAddStudentViewModel model, CancellationToken ct) => AddStudent(model, ct);
    public Task<IActionResult> OnPostRemoveStudentAsync(int examId, int candidateId, CancellationToken ct) => RemoveStudent(examId, candidateId, ct);
    public Task<IActionResult> OnPostRescheduleAsync(ExamSessionRescheduleViewModel model, CancellationToken ct) => Reschedule(model, ct);
    public Task<IActionResult> OnGetSearchStudentsAsync(string? q, CancellationToken ct) => SearchStudents(q, ct);
    public async Task<IActionResult> OnGetSnapshotAsync(int id, CancellationToken ct) { var result = await Details(id, ct); return result is Microsoft.AspNetCore.Mvc.RazorPages.PageResult ? Partial("/Pages/Shared/ExamSessionsDetails.cshtml", Data) : result; }
}
