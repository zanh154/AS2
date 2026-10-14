using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssignmentPRN.Presentation.Pages.Documents;

[StaffPage]
public class IndexModel : PageModel
{
    private readonly IDocumentService _documentService;

    public IndexModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public int CourseId { get; set; }
    public IReadOnlyList<DocumentResponse> Documents { get; set; } = [];
    public string? Error { get; set; }
    public string? Success { get; set; }

    public async Task OnGetAsync(int courseId, CancellationToken cancellationToken)
    {
        CourseId = courseId;

        var response = await _documentService.ListByCourseAsync(courseId, cancellationToken);
        if (response.Success && response.Data is not null)
        {
            Documents = response.Data;
        }
        else
        {
            Error = response.Error;
        }

        Success = TempData["Success"] as string;
        Error ??= TempData["Error"] as string;
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int courseId, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        var response = await _documentService.DeleteAsync(id, userId, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xoá tài liệu."
            : response.Error ?? "Không thể xoá tài liệu.";

        return RedirectToPage(new { courseId });
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
}