using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

public sealed class IndexModel(IExamResultService resultService) : ExamResultsPage(resultService)
{
    public Task<IActionResult> OnGetAsync(int id, CancellationToken ct) => Session(id, ct);

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
