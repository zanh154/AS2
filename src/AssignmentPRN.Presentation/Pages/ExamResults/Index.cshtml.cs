using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

public sealed class IndexModel(
    IExamResultService resultService,
    GradingQueue? gradingQueue = null) : ExamResultsPage(resultService)
{
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var page = await Session(id, ct);
        if (gradingQueue is not null && Data is SessionResultViewModel vm)
        {
            foreach (var candidate in vm.Session.Candidates)
            {
                if (candidate.Status == CandidateStatus.Completed
                    && !candidate.IsCheatingTerminated
                    && candidate.Score is null)
                {
                    try { await gradingQueue.QueueCandidateAsync(candidate.CandidateId, ct); }
                    catch (Exception) { }
                }
            }
        }
        return page;
    }

    public async Task<IActionResult> OnPostCloseOverdueAsync(int id, CancellationToken ct)
    {
        var result = await Results.CloseOverdueSlotsAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? $"Đã chốt ca thi: {result.Data!.MarkedAbsent} sinh viên vắng, "
              + $"{result.Data.Completed} bài chưa nộp được đóng lại."
            : result.Error;

        return RedirectToPage(new { id });
    }
}
