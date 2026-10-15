using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

public sealed class CandidateModel(IExamResultService resultService, IResultReviewService reviewService)
    : ExamResultsPage(resultService)
{
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var paper = await Paper(id, ct);
        if (Data is not CandidateResultViewModel model) return paper;

        // The AI side is extra, not essential: a paper still has to open when grading
        // has not run or the review cannot be read.
        var review = await reviewService.GetAsync(id, LecturerId, ct);
        model.Review = review.Data;
        model.ReviewError = review.Success ? null : review.Error;
        return paper;
    }

    public async Task<IActionResult> OnPostAdjustAsync(int id, decimal? score, string? comment, CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        var result = await reviewService.AdjustAsync(id, userId, score, comment, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? score is null
                ? "Đã bỏ điểm chốt; bài quay lại dùng điểm AI đề xuất."
                : "Đã lưu điểm chốt của giảng viên."
            : result.Error;

        return RedirectToPage(new { id });
    }
}
