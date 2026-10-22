using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.Questions;
public sealed class IndexModel(IQuestionService questions, ICourseMaterialService materials, ICatalogService catalog) : QuestionsPage(questions, materials, catalog)
{
    public async Task<IActionResult> OnGetAsync(
        int? courseId, int? materialId, QuestionDifficulty? difficulty, BloomLevel? bloomLevel,
        string? term, bool includeArchived, CancellationToken ct) =>
        View(await BuildIndexModelAsync(courseId, materialId, difficulty, bloomLevel, term, includeArchived, ct));

    public async Task<IActionResult> OnGetSnapshotAsync(
        int? courseId, int? materialId, QuestionDifficulty? difficulty, BloomLevel? bloomLevel,
        string? term, bool includeArchived, CancellationToken ct) =>
        Partial("/Pages/Shared/QuestionsIndexContent.cshtml",
            await BuildIndexModelAsync(courseId, materialId, difficulty, bloomLevel, term, includeArchived, ct));

    public Task<IActionResult> OnPostArchiveAsync(int id, CancellationToken ct) => Archive(id, ct);
}
