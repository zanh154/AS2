using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.ViewModels;

public class ExamSessionEditViewModel
{
    public int ExamId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Môn học")]
    public int CourseId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên phiên thi."), StringLength(200), Display(Name = "Tên phiên thi")]
    public string ExamName { get; set; } = string.Empty;
    [StringLength(1000), Display(Name = "Mô tả")]
    public string? Description { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ngày giờ."), Display(Name = "Ngày giờ bắt đầu")]
    public DateTime? StartTime { get; set; }
    [Range(1, 1440), Display(Name = "Phút / sinh viên")]
    public int TimePerStudent { get; set; }
    [Range(1, 50), Display(Name = "Số câu hỏi chính")]
    public int MainQuestionCount { get; set; }
    [Range(0, 50), Display(Name = "Số câu hỏi phụ tối đa")]
    public int MaxFollowUpCount { get; set; }
    public List<SelectListItem> Courses { get; set; } = [];
}
