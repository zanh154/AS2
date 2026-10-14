using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents.Questions;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ReviewModel : PageModel
{
    private readonly IDocumentService _documentService;

    public ReviewModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public int DocumentId { get; set; }
    public GeneratedQuestionResponse? Question { get; set; }
    public bool IsNew => Question?.Status == QuestionStatus.PendingReview;
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

        var status = decision == 1 ? QuestionStatus.Approved : QuestionStatus.Rejected;

        var request = new ReviewQuestionRequest(status, notes);
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

    public string GetQuestionStatusBadgeClass(QuestionStatus status) => status switch
    {
        QuestionStatus.PendingReview => "bg-warning text-dark",
        QuestionStatus.Approved => "bg-success",
        QuestionStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    public string GetQuestionStatusText(QuestionStatus status) => status switch
    {
        QuestionStatus.PendingReview => "Chờ duyệt",
        QuestionStatus.Approved => "Đã duyệt",
        QuestionStatus.Rejected => "Đã từ chối",
        _ => "Không xác định"
    };
}