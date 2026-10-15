using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.ViewModels;

public class DocumentListViewModel
{
    public IReadOnlyList<DocumentResponse> Documents { get; init; } = Array.Empty<DocumentResponse>();

    public IReadOnlyList<SelectListItem> CourseOptions { get; init; } = Array.Empty<SelectListItem>();

    public int? CourseId { get; init; }

    public string? LoadError { get; init; }
}

public class DocumentUploadViewModel
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Tệp tài liệu")]
    [Required(ErrorMessage = "Vui lòng chọn tệp tài liệu.")]
    public IFormFile? File { get; set; }

    public string CourseLabel { get; set; } = string.Empty;

    public int MaxFileSizeMB { get; set; } = 50;

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class DocumentDetailsViewModel
{
    public DocumentResponse? Document { get; init; }

    public IReadOnlyList<GeneratedQuestionResponse> Questions { get; init; } = Array.Empty<GeneratedQuestionResponse>();

    public string? LoadError { get; init; }
}

public class DocumentQuestionsViewModel
{
    public int DocumentId { get; init; }

    public IReadOnlyList<GeneratedQuestionResponse> Questions { get; init; } = Array.Empty<GeneratedQuestionResponse>();

    public int? StatusFilter { get; init; }

    public int? BloomFilter { get; init; }

    public string? LoadError { get; init; }
}

public class DocumentQuestionReviewViewModel
{
    public int DocumentId { get; init; }

    public GeneratedQuestionResponse? Question { get; init; }

    public bool IsNew => Question?.Status == GeneratedQuestionStatus.PendingReview;

    public string? LoadError { get; init; }
}

public static class DocumentText
{
    public static string FileSize(long bytes)
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

    public static string Status(DocumentStatus status) => status switch
    {
        DocumentStatus.Pending => "Chờ xử lý",
        DocumentStatus.Processing => "Đang xử lý",
        DocumentStatus.Completed => "Hoàn tất",
        DocumentStatus.Failed => "Lỗi",
        _ => "Không xác định"
    };

    public static string StatusBadge(DocumentStatus status) => status switch
    {
        DocumentStatus.Pending => "bg-warning text-dark",
        DocumentStatus.Processing => "bg-info",
        DocumentStatus.Completed => "bg-success",
        DocumentStatus.Failed => "bg-danger",
        _ => "bg-secondary"
    };

    public static string QuestionStatus(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "Chờ duyệt",
        GeneratedQuestionStatus.Approved => "Đã duyệt",
        GeneratedQuestionStatus.Rejected => "Đã từ chối",
        _ => "Không xác định"
    };

    public static string QuestionStatusBadge(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "bg-warning text-dark",
        GeneratedQuestionStatus.Approved => "bg-success",
        GeneratedQuestionStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    public static string Bloom(DocumentBloomLevel level) => level switch
    {
        DocumentBloomLevel.Remember => "Remember (Nhớ)",
        DocumentBloomLevel.Understand => "Understand (Hiểu)",
        DocumentBloomLevel.Apply => "Apply (Áp dụng)",
        DocumentBloomLevel.Analyze => "Analyze (Phân tích)",
        DocumentBloomLevel.Evaluate => "Evaluate (Đánh giá)",
        DocumentBloomLevel.Create => "Create (Sáng tạo)",
        _ => level.ToString()
    };
}
