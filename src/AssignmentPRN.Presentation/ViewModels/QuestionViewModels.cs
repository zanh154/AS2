using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.ViewModels;

public class QuestionListViewModel
{
    public IReadOnlyList<QuestionListItemResponse> Questions { get; init; } =
        Array.Empty<QuestionListItemResponse>();

    public IReadOnlyList<SelectListItem> CourseOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MaterialOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; init; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BloomOptions { get; init; } = Array.Empty<SelectListItem>();

    public int? CourseId { get; init; }

    public int? MaterialId { get; init; }

    public QuestionDifficulty? Difficulty { get; init; }

    public BloomLevel? BloomLevel { get; init; }

    public string? Term { get; init; }

    public bool IncludeArchived { get; init; }

    public string? LoadError { get; init; }
}

/// <summary>
/// One answer choice row on the question editor. Which row is the right answer is not
/// stored here but in <see cref="QuestionEditViewModel.CorrectIndex"/>, because the
/// radio group posts a single value rather than one flag per row.
/// </summary>
public sealed class QuestionOptionViewModel
{
    // Rows may stay blank: an oral question has none, a multiple-choice one needs two or
    // more. The count is checked by QuestionEditViewModel.Validate, not per row.
    [Display(Name = "Phương án")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(500, ErrorMessage = "Phương án không được vượt quá 500 ký tự.")]
    public string Text { get; set; } = string.Empty;
}

public class QuestionEditViewModel : IValidatableObject
{
    /// <summary>Fixed number of choice rows the editor renders.</summary>
    public const int OptionRows = 4;

    public int QuestionId { get; set; }

    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Chủ đề (tài liệu)")]
    public int? MaterialId { get; set; }

    [Display(Name = "Nội dung câu hỏi")]
    [Required(ErrorMessage = "Vui lòng nhập nội dung câu hỏi.")]
    [StringLength(4000, ErrorMessage = "Nội dung câu hỏi không được vượt quá 4000 ký tự.")]
    public string QuestionText { get; set; } = string.Empty;

    [Display(Name = "Đáp án mong đợi")]
    [StringLength(4000, ErrorMessage = "Đáp án mong đợi không được vượt quá 4000 ký tự.")]
    public string? ExpectedAnswer { get; set; }

    [Display(Name = "Mức độ khó")]
    public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

    [Display(Name = "Mức độ Bloom")]
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;

    [Display(Name = "Loại câu hỏi")]
    public QuestionType QuestionType { get; set; } = QuestionType.Main;

    public List<QuestionOptionViewModel> Options { get; set; } = [];

    /// <summary>
    /// Row number of the correct answer, as posted by the radio group. -1 means the
    /// lecturer has not picked one yet.
    /// </summary>
    [Display(Name = "Phương án đúng")]
    public int CorrectIndex { get; set; } = -1;

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> MaterialOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BloomOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> QuestionTypeOptions { get; set; } = Array.Empty<SelectListItem>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CourseId <= 0)
        {
            yield return new ValidationResult("Vui lòng chọn môn học.", [nameof(CourseId)]);
        }

        if (QuestionType == QuestionType.FollowUp && MaterialId is null or <= 0)
        {
            yield return new ValidationResult(
                "Câu hỏi đào sâu phải gắn với một chủ đề (tài liệu).", [nameof(MaterialId)]);
        }

        var filled = Options.Count(option => !string.IsNullOrWhiteSpace(option.Text));
        // No choices at all makes it an oral question, graded against the expected answer.
        if (filled == 0 && string.IsNullOrWhiteSpace(ExpectedAnswer))
        {
            yield return new ValidationResult(
                "Câu hỏi vấn đáp (không có phương án) cần có đáp án mong đợi.", [nameof(ExpectedAnswer)]);
        }
        else if (filled == 1)
        {
            yield return new ValidationResult("Câu hỏi trắc nghiệm cần ít nhất 2 phương án trả lời.", [nameof(Options)]);
        }

        // The pick must land on a row that actually has text, so ticking an answer and
        // then clearing its box cannot slip through.
        var picked = CorrectIndex >= 0 && CorrectIndex < Options.Count
            && !string.IsNullOrWhiteSpace(Options[CorrectIndex].Text);
        if (filled >= 2 && !picked)
        {
            yield return new ValidationResult(
                "Vui lòng tick vào ô Đúng của phương án đúng.", [nameof(CorrectIndex)]);
        }
    }

    /// <summary>Expands the option list to the fixed number of rows the form binds to.</summary>
    public static List<QuestionOptionViewModel> BuildOptions(int count = OptionRows)
    {
        var options = new List<QuestionOptionViewModel>(count);
        for (var index = 0; index < count; index++)
        {
            options.Add(new QuestionOptionViewModel());
        }

        return options;
    }
}

public class CourseMaterialListViewModel
{
    public IReadOnlyList<CourseMaterialResponse> Materials { get; init; } =
        Array.Empty<CourseMaterialResponse>();

    public IReadOnlyList<SelectListItem> CourseOptions { get; init; } = Array.Empty<SelectListItem>();

    public int? CourseId { get; init; }

    public string? LoadError { get; init; }
}

/// <summary>
/// Fixes a material filed under the wrong course, and renames it. The file itself is
/// never re-uploaded here, so the extension is fixed and shown read-only.
/// </summary>
public class CourseMaterialEditViewModel
{
    public int MaterialId { get; set; }

    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Tên tệp")]
    [Required(ErrorMessage = "Vui lòng nhập tên tệp.")]
    [StringLength(255, ErrorMessage = "Tên tệp không được vượt quá 255 ký tự.")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Course the material sits in right now, shown so the move is obvious.</summary>
    public string CurrentCourseLabel { get; set; } = string.Empty;

    /// <summary>Questions filed under this topic; while this is above zero the course is locked.</summary>
    public int QuestionCount { get; set; }

    public bool CanChangeCourse => QuestionCount == 0;

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class CourseMaterialUploadViewModel
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Tệp tài liệu")]
    [Required(ErrorMessage = "Vui lòng chọn tệp tài liệu.")]
    public IFormFile? File { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class QuestionImportViewModel
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "File CSV")]
    [Required(ErrorMessage = "Vui lòng chọn file CSV.")]
    public IFormFile? File { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();

    public QuestionImportResult? Result { get; set; }
}

public static class QuestionText
{
    public static string Difficulty(QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "Dễ",
        QuestionDifficulty.Medium => "Trung bình",
        QuestionDifficulty.Hard => "Khó",
        _ => difficulty.ToString()
    };

    public static string Bloom(BloomLevel level) => level switch
    {
        BloomLevel.Remember => "Nhớ",
        BloomLevel.Understand => "Hiểu",
        BloomLevel.Apply => "Áp dụng",
        BloomLevel.Analyze => "Phân tích",
        _ => level.ToString()
    };

    public static string Status(QuestionStatus status) => status switch
    {
        QuestionStatus.Approved => "Sẵn sàng",
        QuestionStatus.Archived => "Đã ẩn",
        QuestionStatus.Draft => "Nháp",
        QuestionStatus.PendingReview => "Chờ duyệt",
        QuestionStatus.Rejected => "Bị từ chối",
        _ => status.ToString()
    };

    public static string Type(QuestionType type) => type switch
    {
        QuestionType.Main => "Câu chính",
        QuestionType.FollowUp => "Đào sâu",
        _ => type.ToString()
    };

    public static string Source(QuestionSource source) => source switch
    {
        QuestionSource.AI => "AI",
        QuestionSource.Manual => "Thủ công",
        _ => source.ToString()
    };

    public static string DifficultyChip(QuestionDifficulty difficulty) => difficulty switch
    {
        QuestionDifficulty.Easy => "status-chip--success",
        QuestionDifficulty.Hard => "status-chip--danger",
        _ => "status-chip--warning"
    };

    public static string FileType(MaterialFileType type) => type switch
    {
        MaterialFileType.PDF => "PDF",
        MaterialFileType.DOCX => "Word",
        MaterialFileType.PPTX => "PowerPoint",
        _ => type.ToString()
    };

    public static string FileSize(long? bytes) => bytes switch
    {
        null or 0 => "—",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB"
    };
}
