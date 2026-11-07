using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Courses;
public sealed class CreateModel(ICourseService service, ICatalogService catalog) : CoursesPage(service, catalog)
{
    public Task<IActionResult> OnGetAsync(CancellationToken ct) => Create(ct);
    public Task<IActionResult> OnPostAsync(CourseViewModel model, CancellationToken ct) => Save(model, ct);
}
