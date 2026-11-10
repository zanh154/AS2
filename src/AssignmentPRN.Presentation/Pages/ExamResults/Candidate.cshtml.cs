using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentPRN.Presentation.Pages.ExamResults;

public sealed class CandidateModel(
    IExamResultService resultService,
    IResultReviewService reviewService,
    IGradingService? gradingService = null)
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

    public async Task<IActionResult> OnPostAdjustAsync(
        int id,
        decimal? score,
        string? comment,
        int[]? questionOrder,
        decimal?[]? questionScore,
        bool clearOverride,
        CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        List<QuestionScoreAdjustment>? perQuestion = null;
        if (questionOrder is not null && questionScore is not null)
        {
            perQuestion = [];
            var count = Math.Min(questionOrder.Length, questionScore.Length);
            for (var i = 0; i < count; i++)
            {
                if (questionScore[i] is decimal qScore)
                {
                    perQuestion.Add(new QuestionScoreAdjustment(
                        questionOrder[i],
                        Math.Round(Math.Clamp(qScore, 0m, 10m), 2, MidpointRounding.AwayFromZero)));
                }
            }
        }

        decimal? finalScoreInput = clearOverride ? null : score;
        if (!clearOverride && finalScoreInput is null && perQuestion is { Count: > 0 })
        {
            finalScoreInput = Math.Round(perQuestion.Average(x => x.Score), 2, MidpointRounding.AwayFromZero);
        }

        var result = await reviewService.AdjustAsync(id, userId, finalScoreInput, comment, perQuestion, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? finalScoreInput is null
                ? "Đã bỏ điểm chốt; bài quay lại dùng điểm AI đề xuất."
                : "Đã lưu điểm chốt của giảng viên."
            : result.Error;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGradeNowAsync(int id, CancellationToken ct)
    {
        var paper = await Paper(id, ct);
        if (Data is not CandidateResultViewModel) return paper;

        if (gradingService is null)
        {
            TempData["Error"] = "Dịch vụ chấm AI chưa được cấu hình.";
            return RedirectToPage(new { id });
        }

        var graded = await gradingService.GradeCandidateAsync(id, ct);
        TempData[graded.Success ? "Success" : "Error"] = graded.Success
            ? $"AI đã chấm xong {graded.Data!.GradedAnswers} câu (bỏ qua {graded.Data.SkippedAnswers} câu)."
            : graded.Error;

        return RedirectToPage(new { id });
    }
}
