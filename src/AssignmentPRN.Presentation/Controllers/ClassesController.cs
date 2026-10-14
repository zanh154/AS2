using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ClassesController(
    IAcademicClassService service,
    ICatalogService catalog, Microsoft.AspNetCore.SignalR.IHubContext<AssignmentPRN.Presentation.Hubs.ExamUpdatesHub> updatesHub) : Controller
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await service.ListAsync(LecturerId, ct);
        if (!result.Success) TempData["Error"] = result.Error;
        return View(result.Data ?? []);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new AcademicClassViewModel { LecturerId = LecturerId ?? 0 };
        await Options(model, ct);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }
        var item = result.Data;
        var model = new AcademicClassViewModel
        {
            ClassId = item.ClassId,
            ClassCode = item.ClassCode,
            ClassName = item.ClassName,
            CourseId = item.CourseId,
            LecturerId = item.LecturerId,
            IsActive = item.IsActive
        };
        await Options(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AcademicClassViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue) { model.LecturerId = LecturerId.Value; ModelState.Remove(nameof(model.LecturerId)); }
        if (ModelState.IsValid)
        {
            var result = await service.SaveAsync(new(model.ClassId, model.ClassCode, model.ClassName,
                model.CourseId, model.LecturerId, model.IsActive), LecturerId, ct);
            if (result.Success && result.Data is not null)
            {
                TempData["Success"] = "Đã lưu lớp học.";
                return RedirectToAction(nameof(Roster), new { id = result.Data.ClassId });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await Options(model, ct);
        return View("Edit", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xoá lớp học." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The roster screen where students are added to and removed from the class.</summary>
    [HttpGet]
    public async Task<IActionResult> Roster(int id, CancellationToken ct)
    {
        var result = await service.GetRosterAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }

        var enrolled = result.Data.Students.Select(x => x.StudentId).ToHashSet();
        var studentResult = await catalog.ListActiveUsersInRoleAsync(RoleNames.Student, ct);
        var students = studentResult.Data ?? [];
        return View(new ClassRosterViewModel
        {
            Class = result.Data.Class,
            Students = result.Data.Students,
            AvailableStudents = students
                .Where(x => !enrolled.Contains(x.UserId))
                .Select(x => new SelectListItem($"{x.FullName} · {x.Email}", x.UserId.ToString()))
                .ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStudent(int id, int studentId, CancellationToken ct)
    {
        var result = await service.AddStudentAsync(id, studentId, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã thêm sinh viên vào lớp." : result.Error;
        if (result.Success) await updatesHub.Clients.All.SendCoreAsync("ExamListChanged", [new { classId = id, message = "Danh sách lớp vừa thay đổi." }], ct);
        return RedirectToAction(nameof(Roster), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudent(int id, int studentId, CancellationToken ct)
    {
        var result = await service.RemoveStudentAsync(id, studentId, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xoá sinh viên khỏi lớp." : result.Error;
        if (result.Success) await updatesHub.Clients.All.SendCoreAsync("ExamListChanged", [new { classId = id, message = "Danh sách lớp vừa thay đổi." }], ct);
        return RedirectToAction(nameof(Roster), new { id });
    }

    [HttpGet]
    public Task<IActionResult> Details(int id, CancellationToken ct) => Roster(id, ct);

    private async Task Options(AcademicClassViewModel model, CancellationToken ct)
    {
        model.IsLecturerFixed = LecturerId.HasValue;
        // A class inherits its course's lecturer, so only that lecturer's active courses can be picked.
        var courseResult = await catalog.ListActiveCoursesAsync(ct);
        model.Courses = (courseResult.Data ?? [])
            .Where(x => !LecturerId.HasValue || x.LecturerId == LecturerId)
            .Select(x => new SelectListItem($"{x.CourseCode} · {x.CourseName}", x.CourseId.ToString()))
            .ToList();
        var lecturerResult = await catalog.ListActiveUsersInRoleAsync(RoleNames.Lecturer, ct);
        model.Lecturers = (lecturerResult.Data ?? [])
            .Where(x => !LecturerId.HasValue || x.UserId == LecturerId)
            .Select(x => new SelectListItem(x.FullName, x.UserId.ToString()))
            .ToList();
    }
}
