using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Questions;
public sealed class CreateModel(IQuestionService questions, ICourseMaterialService materials, ICatalogService catalog) : QuestionsPage(questions, materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int? courseId, CancellationToken ct) => Create(courseId, ct);
}
