using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents.Questions;

[StaffPage]
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

        GeneratedQuestionStatus? statusEnum = status.HasValue ? (GeneratedQuestionStatus)status.Value : null;

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