using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.ViewModels;

public class ExamSessionListViewModel
{
    public IReadOnlyList<ExamSessionListItemResponse> Sessions { get; init; } =
        Array.Empty<ExamSessionListItemResponse>();

    public string? LoadError { get; init; }
}

public class ExamSessionDetailViewModel
{
    public ExamSessionDetailResponse Session { get; init; } = new();
}

public class ExamSessionCreateViewModel : IValidatableObject
{
    [Display(Name = "Môn học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học.")]
    public int CourseId { get; set; }

    [Display(Name = "Giảng viên")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn giảng viên.")]
    public int LecturerId { get; set; }

    [Display(Name = "Lớp học")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn lớp học.")]
    public int ClassId { get; set; }

    [Display(Name = "Tên lịch thi")]
    [Required(ErrorMessage = "Vui lòng nhập tên lịch thi.")]
    [StringLength(200, ErrorMessage = "Tên lịch thi không được vượt quá 200 ký tự.")]
    public string ExamName { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Bắt đầu")]
    [Required(ErrorMessage = "Vui lòng chọn ngày giờ bắt đầu.")]
    public DateTime? StartTime { get; set; } = DateTime.Today.AddDays(1).AddHours(8);

    [Display(Name = "Thời lượng mỗi sinh viên (phút)")]
    [Range(1, 1440, ErrorMessage = "Thời lượng phải từ 1 đến 1440 phút.")]
    public int TimePerStudent { get; set; } = 20;

    [Display(Name = "Số câu hỏi chính")]
    [Range(1, 50, ErrorMessage = "Số câu hỏi chính phải từ 1 đến 50.")]
    public int MainQuestionCount { get; set; } = 3;

    [Display(Name = "Số câu hỏi phụ tối đa")]
    [Range(0, 50, ErrorMessage = "Số câu hỏi phụ tối đa phải từ 0 đến 50.")]
    public int MaxFollowUpCount { get; set; } = 2;

    public IReadOnlyList<SelectListItem> CourseOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> LecturerOptions { get; set; } = Array.Empty<SelectListItem>();

    public bool IsLecturerFixed { get; set; }

    public IReadOnlyList<SelectListItem> ClassOptions { get; set; } = Array.Empty<SelectListItem>();

    public string? OptionsError { get; set; }

    public bool CanCreate => CourseOptions.Count > 0 && LecturerOptions.Count > 0 && ClassOptions.Count > 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime.HasValue && StartTime.Value < DateTime.Now)
        {
            yield return new ValidationResult(
                "Giờ bắt đầu không được nằm trong quá khứ.",
                [nameof(StartTime)]);
        }

    }
}

public sealed class SearchableSelectViewModel
{
    public required string Name { get; init; }

    /// <summary>Selected id, for the pickers whose options are keyed by a database id.</summary>
    public int SelectedValue { get; init; }

    /// <summary>
    /// Selected value for options that are not ids — an enum name, say. Takes precedence
    /// over <see cref="SelectedValue"/> so both kinds of picker share one partial.
    /// </summary>
    public string? SelectedKey { get; init; }

    /// <summary>
    /// The initial-letter bubble in front of each option. It reads well for people and
    /// courses; a short fixed list such as a difficulty is cleaner without it.
    /// </summary>
    public bool ShowAvatar { get; init; } = true;
    public required string Placeholder { get; init; }

    public required string SearchPlaceholder { get; init; }

    public required string EmptyText { get; init; }

    public IReadOnlyList<SelectListItem> Options { get; init; } = Array.Empty<SelectListItem>();

    /// <summary>
    /// Extra data-* attributes for the underlying select (e.g. data-roster-status),
    /// so existing scripts keep finding it. Keys must be valid attribute names.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; }
        = new Dictionary<string, string>();
}

public sealed class DateTimePickerViewModel
{
    /// <summary>The name attribute of the hidden input bound to the selected value.</summary>
    public required string Name { get; init; }

    /// <summary>Initial value formatted as yyyy-MM-ddTHH:mm (ISO 8601 local).</summary>
    public string? Value { get; init; }

    /// <summary>Label shown in the trigger button when a value is already selected.</summary>
    public string? DisplayLabel { get; init; }

    /// <summary>Placeholder text when no value is selected.</summary>
    public string Placeholder { get; init; } = "Chọn ngày và giờ";

    /// <summary>
    /// Earliest selectable moment. Defaults to "now" when null. Days before it are
    /// disabled and the clock snaps forward, so the picker cannot return a value
    /// the server would reject.
    /// </summary>
    public DateTime? Earliest { get; init; }

    /// <summary>
    /// Latest selectable moment, e.g. the last start that still ends inside the
    /// exam day. Null leaves the upper end open.
    /// </summary>
    public DateTime? Latest { get; init; }

    /// <summary>
    /// Drops the clock and posts <c>yyyy-MM-dd</c>. For date filters, where a
    /// time of day would be noise the server only throws away.
    /// </summary>
    public bool DateOnly { get; init; }

    /// <summary>
    /// Lifts the "not before now" floor. Scheduling pickers must keep it; a
    /// filter over past exams cannot work with it.
    /// </summary>
    public bool AllowPast { get; init; }

    /// <summary>Adds a "Xoá" button, so an optional filter can be emptied again.</summary>
    public bool Clearable { get; init; }

    /// <summary>
    /// Extra data-* attributes to add to the hidden input (e.g. data-schedule-start).
    /// Each key should be a valid HTML attribute name.
    /// </summary>
    public IReadOnlyDictionary<string, string> InputAttributes { get; init; }
        = new Dictionary<string, string>();
}

public class ExamSessionAddStudentViewModel
{
    [Range(1, int.MaxValue)]
    public int ExamId { get; set; }

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public DateTime? ScheduledTime { get; set; }
}

public class ExamSessionRescheduleViewModel
{
    [Range(1, int.MaxValue)]
    public int CandidateId { get; set; }

    [Range(1, int.MaxValue)]
    public int ExamId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu mới.")]
    public DateTime? ScheduledTime { get; set; }
}

public class StudentScheduleViewModel
{
    public StudentScheduleResponse? Schedule { get; set; }

    public string? Error { get; set; }

    /// <summary>True when a student views their own schedule instead of staff looking one up.</summary>
    public bool IsOwnSchedule { get; set; }

    [Display(Name = "Email sinh viên")]
    [StringLength(255)]
    public string Query { get; set; } = string.Empty;

    public bool HasSearched { get; set; }

    /// <summary>Populated when the search matched more than one student.</summary>
    public IReadOnlyList<PersonResponse> Matches { get; set; } = Array.Empty<PersonResponse>();
}

public static class ExamSessionText
{
    public static string Status(ExamSessionStatus status) => status switch
    {
        ExamSessionStatus.Draft => "Nháp",
        ExamSessionStatus.Scheduled => "Đã xếp lịch",
        ExamSessionStatus.InProgress => "Đang diễn ra",
        ExamSessionStatus.Completed => "Đã hoàn thành",
        ExamSessionStatus.Cancelled => "Đã huỷ",
        _ => status.ToString()
    };

    public static string Status(CandidateStatus status) => status switch
    {
        CandidateStatus.Waiting => "Chờ thi",
        CandidateStatus.InProgress => "Đang thi",
        CandidateStatus.Completed => "Đã thi",
        CandidateStatus.Absent => "Vắng",
        CandidateStatus.Cancelled => "Đã huỷ",
        _ => status.ToString()
    };

    public static string ChipModifier(ExamSessionStatus status) => status switch
    {
        ExamSessionStatus.Completed => "status-chip--success",
        ExamSessionStatus.InProgress => "status-chip--warning",
        ExamSessionStatus.Cancelled => "status-chip--danger",
        ExamSessionStatus.Draft => "status-chip--muted",
        _ => string.Empty
    };

    public static string ChipModifier(CandidateStatus status) => status switch
    {
        CandidateStatus.Completed => "status-chip--success",
        CandidateStatus.InProgress => "status-chip--warning",
        CandidateStatus.Absent or CandidateStatus.Cancelled => "status-chip--danger",
        _ => string.Empty
    };
}
