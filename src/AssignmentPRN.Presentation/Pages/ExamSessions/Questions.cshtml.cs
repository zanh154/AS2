using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace AssignmentPRN.Presentation.Pages.ExamSessions;
public sealed class QuestionsModel(IExamSessionService sessions, IQuestionService questions, ICourseMaterialService materials, IExamRealtimeNotifier updates) : ExamSessionsPage(sessions, questions, materials, updates)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct, List<int>? materialIds = null, List<QuestionDifficulty>? difficulties = null, bool preview = false) => Questions(id, ct, materialIds, difficulties, preview);
    public Task<IActionResult> OnPostAssignQuestionsAsync(ExamQuestionAssignViewModel model, CancellationToken ct) => AssignQuestions(model, ct);
    public Task<IActionResult> OnPostClearQuestionsAsync(int id, CancellationToken ct) => ClearQuestions(id, ct);
}
