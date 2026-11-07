using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.ViewModels;

public class CourseViewModel
{
    public int CourseId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mã môn."), StringLength(50), Display(Name = "Mã môn")]
    public string CourseCode { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập tên môn."), StringLength(200), Display(Name = "Tên môn")]
    public string CourseName { get; set; } = string.Empty;
    [StringLength(1000), Display(Name = "Mô tả")]
    public string? Description { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn giảng viên."), Display(Name = "Giảng viên phụ trách")]
    public int LecturerId { get; set; }
    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
    public List<SelectListItem> Lecturers { get; set; } = [];
}
