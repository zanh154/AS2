using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

public sealed class CandidateModel(IExamResultService resultService) : ExamResultsPage(resultService)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Paper(id, ct);
}
