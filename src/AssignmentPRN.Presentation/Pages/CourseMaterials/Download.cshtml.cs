using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.CourseMaterials;
public sealed class DownloadModel(ICourseMaterialService materials, ICatalogService catalog) : CourseMaterialsPage(materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Download(id, ct);
}
