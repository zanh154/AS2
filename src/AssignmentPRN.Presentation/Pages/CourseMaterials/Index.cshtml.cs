using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.CourseMaterials;
public sealed class IndexModel(ICourseMaterialService materials, ICatalogService catalog) : CourseMaterialsPage(materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int? courseId, CancellationToken ct) => Index(courseId, ct);
    public Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct) => Delete(id, ct);
}
