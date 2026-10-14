using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents.Questions;

[StaffPage]
public class ReviewModel : PageModel
{
    private readonly IDocumentService _documentService;

    public ReviewModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public int DocumentId { get; set; }
    public GeneratedQuestionResponse? Question { get; set; }
    public bool IsNew => Question?.Status == GeneratedQuestionStatus.PendingReview;
    public string? Error { get; set; }
    public string? Success { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int documentId, CancellationToken cancellationToken)
    {
        DocumentId = documentId;

        var response = await _documentService.GetQuestionAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            Error = response.Error;
            return Page();
        }

        Question = response.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        int questionId,
        int documentId,
        string questionText,
        string? answerKey,
        string? explanation,
        int bloomLevel,
        string? topic,
        int decision, // 1 = Approve, 2 = Reject
        string? notes,
        CancellationToken cancellationToken)
    {
        DocumentId = documentId;

        var reviewerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        if (decision is not (1 or 2)) return BadRequest();
        var status = decision == 1 ? GeneratedQuestionStatus.Approved : GeneratedQuestionStatus.Rejected;

        var request = new ReviewQuestionRequest(status, notes, questionText, answerKey, explanation, (DocumentBloomLevel)bloomLevel, topic);
        var response = await _documentService.ReviewQuestionAsync(questionId, reviewerId, request, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = decision == 1 ? "Đã duyệt câu hỏi." : "Đã từ chối câu hỏi.";
        }
        else
        {
            TempData["Error"] = response.Error;
        }

        return RedirectToPage("Index", new { documentId });
    }

    public string GetQuestionStatusBadgeClass(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "bg-warning text-dark",
        GeneratedQuestionStatus.Approved => "bg-success",
        GeneratedQuestionStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    public string GetQuestionStatusText(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "Chờ duyệt",
        GeneratedQuestionStatus.Approved => "Đã duyệt",
        GeneratedQuestionStatus.Rejected => "Đã từ chối",
        _ => "Không xác định"
    };
}