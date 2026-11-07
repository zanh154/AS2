using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.CourseMaterials;
public sealed class EditModel(ICourseMaterialService materials, ICatalogService catalog) : CourseMaterialsPage(materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Edit(id, ct);
    public Task<IActionResult> OnPostAsync(CourseMaterialEditViewModel model, CancellationToken ct) => Edit(model, ct);
}
