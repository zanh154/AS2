using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Pages.Classes;
public sealed class EditModel(IAcademicClassService service, ICatalogService catalog, IHubContext<ExamUpdatesHub> hub) : ClassesPage(service, catalog, hub)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Edit(id, ct);
    public Task<IActionResult> OnPostAsync(AcademicClassViewModel model, CancellationToken ct) => Save(model, ct);
}
