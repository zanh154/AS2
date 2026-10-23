using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
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
        DocumentId = Question.DocumentId;
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
        var current = await _documentService.GetQuestionAsync(questionId, cancellationToken);
        if (!current.Success || current.Data is null) return NotFound();
        DocumentId = current.Data.DocumentId;
        Question = current.Data with { QuestionText = questionText ?? string.Empty, AnswerKey = answerKey, Explanation = explanation, Topic = topic };
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(questionText))
        {
            Error = "Vui lòng nhập nội dung câu hỏi hợp lệ.";
            return Page();
        }
        var reviewerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        if (decision is not (1 or 2)) return BadRequest();
        var status = decision == 1 ? GeneratedQuestionStatus.Approved : GeneratedQuestionStatus.Rejected;

        var request = new ReviewQuestionRequest(status, notes, questionText, answerKey ?? string.Empty, explanation ?? string.Empty, (DocumentBloomLevel)bloomLevel, topic ?? string.Empty);
        var response = await _documentService.ReviewQuestionAsync(questionId, reviewerId, request, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = decision == 1 ? "Đã duyệt câu hỏi." : "Đã từ chối câu hỏi.";
        }
        else
        {
            Error = response.Error;
            return Page();
        }

        return RedirectToPage("Index", new { documentId = DocumentId });
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