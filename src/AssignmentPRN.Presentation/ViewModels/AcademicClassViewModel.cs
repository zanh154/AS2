using AssignmentPRN.Business.Services;
using System.ComponentModel.DataAnnotations;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.ViewModels;

public class AcademicClassViewModel
{
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã lớp."), StringLength(50), Display(Name = "Mã lớp")]
    public string ClassCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên lớp."), StringLength(200), Display(Name = "Tên lớp")]
    public string ClassName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn môn học."), Display(Name = "Môn học")]
    public int CourseId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn giảng viên."), Display(Name = "Giảng viên phụ trách")]
    public int LecturerId { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public List<SelectListItem> Courses { get; set; } = [];

    public List<SelectListItem> Lecturers { get; set; } = [];

    public bool IsLecturerFixed { get; set; }
}

/// <summary>The roster screen: the class plus the students that can still be added to it.</summary>
public class ClassRosterViewModel
{
    public AcademicClassResponse Class { get; set; } = new();

    public IReadOnlyList<ClassStudentResponse> Students { get; set; } = [];

    public List<SelectListItem> AvailableStudents { get; set; } = [];
}
