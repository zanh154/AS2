using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Questions;
// Razor Pages only take filters on the page class, so the size limit covers the GET too;
// that is harmless, since a GET carries no body.
[RequestSizeLimit(CsvImportRules.MaxBytes + (1024 * 1024))]
public sealed class ImportModel(IQuestionService questions, ICourseMaterialService materials, ICatalogService catalog) : QuestionsPage(questions, materials, catalog)
{
    public Task<IActionResult> OnGetAsync(int? courseId, CancellationToken ct) => Import(courseId, ct);
    public Task<IActionResult> OnPostAsync(QuestionImportViewModel model, CancellationToken ct) => Import(model, ct);
}
