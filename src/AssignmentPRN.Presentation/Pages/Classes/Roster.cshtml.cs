using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Pages.Classes;
public sealed class RosterModel(IAcademicClassService service, ICatalogService catalog, IHubContext<ExamUpdatesHub> hub) : ClassesPage(service, catalog, hub)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Roster(id, ct);
    public Task<IActionResult> OnPostAddStudentAsync(int id, int studentId, CancellationToken ct) => AddStudent(id, studentId, ct);
    public Task<IActionResult> OnPostRemoveStudentAsync(int id, int studentId, CancellationToken ct) => RemoveStudent(id, studentId, ct);
}
