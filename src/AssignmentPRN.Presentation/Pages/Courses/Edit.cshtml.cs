using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Courses;
public sealed class EditModel(ICourseService service, ICatalogService catalog) : CoursesPage(service, catalog)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Edit(id, ct);
    public Task<IActionResult> OnPostAsync(CourseViewModel model, CancellationToken ct) => Save(model, ct);
}
