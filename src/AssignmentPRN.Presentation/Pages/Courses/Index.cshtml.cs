using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Courses;
public sealed class IndexModel(ICourseService service, ICatalogService catalog) : CoursesPage(service, catalog)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => Index(ct);
    public Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) => Delete(id, ct);
    public async Task<IActionResult> OnGetSnapshotAsync(CancellationToken ct) { await Index(ct); return Partial("/Pages/Shared/CoursesIndex.cshtml", Data); }
}
