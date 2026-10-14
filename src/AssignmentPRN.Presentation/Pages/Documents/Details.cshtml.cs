using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents;

[StaffPage]
public class DetailsModel : PageModel
{
    private readonly IDocumentService _documentService;

    public DetailsModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public DocumentResponse? Document { get; set; }
    public IReadOnlyList<GeneratedQuestionResponse> Questions { get; set; } = [];
    public int ProgressPercent { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var response = await _documentService.GetAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            Error = response.Error;
            return Page();
        }

        Document = response.Data;

        if (Document.Status == DocumentStatus.Completed)
        {
            var questionsResponse = await _documentService.ListQuestionsByDocumentAsync(id, cancellationToken);
            if (questionsResponse.Success && questionsResponse.Data is not null)
            {
                Questions = questionsResponse.Data;
            }
        }

        // Simulate progress for processing state
        if (Document.Status == DocumentStatus.Processing)
        {
            ProgressPercent = new Random().Next(10, 90);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateQuestionsAsync(
        int documentId,
        int count,
        int? bloomLevel,
        string? topic,
        CancellationToken cancellationToken)
    {
        var request = new GenerateQuestionsRequest(
            Count: count,
            BloomLevel: bloomLevel.HasValue ? (DocumentBloomLevel)bloomLevel.Value : null,
            Topic: topic
        );

        var response = await _documentService.GenerateQuestionsAsync(documentId, request, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = $"Đã sinh {response.Data?.Count ?? 0} câu hỏi mới.";
        }
        else
        {
            TempData["Error"] = response.Error;
        }

        return RedirectToPage(new { id = documentId });
    }

    public string FormatFileSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        int unit = 0;
        double size = bytes;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    public string GetStatusBadgeClass(DocumentStatus status) => status switch
    {
        DocumentStatus.Pending => "bg-warning text-dark",
        DocumentStatus.Processing => "bg-info",
        DocumentStatus.Completed => "bg-success",
        DocumentStatus.Failed => "bg-danger",
        _ => "bg-secondary"
    };

    public string GetStatusText(DocumentStatus status) => status switch
    {
        DocumentStatus.Pending => "Chờ xử lý",
        DocumentStatus.Processing => "Đang xử lý",
        DocumentStatus.Completed => "Hoàn tất",
        DocumentStatus.Failed => "Lỗi",
        _ => "Không xác định"
    };

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