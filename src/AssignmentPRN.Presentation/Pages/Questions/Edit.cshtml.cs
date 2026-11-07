using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Questions;
public sealed class EditModel(IQuestionService questions, ICourseMaterialService materials, ICatalogService catalog) : QuestionsPage(questions, materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Edit(id, ct);
    public Task<IActionResult> OnPostAsync(QuestionEditViewModel model, CancellationToken ct) => Save(model, ct);
}
