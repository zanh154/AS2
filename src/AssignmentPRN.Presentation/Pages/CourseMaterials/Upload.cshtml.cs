using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.CourseMaterials;
// Razor Pages only take filters on the page class, so the size limit covers the GET too;
// that is harmless, since a GET carries no body.
[RequestSizeLimit(MaterialFileRules.MaxBytes + (1024 * 1024))]
public sealed class UploadModel(ICourseMaterialService materials, ICatalogService catalog) : CourseMaterialsPage(materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int? courseId, CancellationToken ct) => Upload(courseId, ct);
    public Task<IActionResult> OnPostAsync(CourseMaterialUploadViewModel model, CancellationToken ct) => Upload(model, ct);
}
