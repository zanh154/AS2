using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Pages.Classes;
public sealed class IndexModel(IAcademicClassService service, ICatalogService catalog, IHubContext<ExamUpdatesHub> hub) : ClassesPage(service, catalog, hub)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => Index(ct);
    public Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) => Delete(id, ct);
}
