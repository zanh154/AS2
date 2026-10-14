using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents.Questions;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class IndexModel : PageModel
{
    private readonly IDocumentService _documentService;

    public IndexModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public int DocumentId { get; set; }
    public int? StatusFilter { get; set; }
    public int? BloomFilter { get; set; }
    public IReadOnlyList<GeneratedQuestionResponse> Questions { get; set; } = [];
    public string? Error { get; set; }
    public string? Success { get; set; }

    public async Task OnGetAsync(int documentId, int? status, int? bloomLevel, CancellationToken cancellationToken)
    {
        DocumentId = documentId;
        StatusFilter = status;
        BloomFilter = bloomLevel;

        QuestionStatus? statusEnum = status.HasValue ? (QuestionStatus)status.Value : null;

        var response = await _documentService.ListQuestionsByDocumentAsync(documentId, cancellationToken);
        if (response.Success && response.Data is not null)
        {
            var questions = response.Data;

            if (statusEnum.HasValue)
            {
                questions = questions.Where(q => q.Status == statusEnum.Value).ToList();
            }

            if (bloomLevel.HasValue)
            {
                questions = questions.Where(q => (int)q.BloomLevel == bloomLevel.Value).ToList();
            }

            Questions = questions;
        }
        else
        {
            Error = response.Error;
        }

        Success = TempData["Success"] as string;
        Error ??= TempData["Error"] as string;
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