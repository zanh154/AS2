using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Courses;

[StaffPage]
public abstract class CoursesPage(ICourseService service, ICatalogService catalog) : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public object Data { get; private set; } = null!;
    protected IActionResult View(object model) { Data = model; return Page(); }
    protected IActionResult View(string name, object model) => View(model);
    protected IActionResult Go(string action, object? values = null) => RedirectToPage("/Courses/" + action, values);
    protected JsonResult Json(object value) => new(value);

    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    protected async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await service.ListAsync(LecturerId, ct);
        if (!result.Success) TempData["Error"] = result.Error;
        return View(result.Data ?? []);
    }

    protected async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new CourseViewModel { LecturerId = LecturerId ?? 0 };
        await Options(model, ct);
        return View("Edit", model);
    }

    protected async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null) { TempData["Error"] = result.Error; return Go(nameof(Index)); }
        var course = result.Data;
        var model = new CourseViewModel { CourseId = course.CourseId, CourseCode = course.CourseCode,
            CourseName = course.CourseName, Description = course.Description, LecturerId = course.LecturerId, IsActive = course.IsActive };
        await Options(model, ct);
        return View(model);
    }

    protected async Task<IActionResult> Save(CourseViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue) { model.LecturerId = LecturerId.Value; ModelState.Remove(nameof(model.LecturerId)); }
        if (ModelState.IsValid)
        {
            var result = await service.SaveAsync(new(model.CourseId, model.CourseCode, model.CourseName,
                model.Description, model.LecturerId, model.IsActive), LecturerId, ct);
            if (result.Success) { TempData["Success"] = "Đã lưu môn học."; return Go(nameof(Index)); }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await Options(model, ct);
        return View("Edit", model);
    }

    protected async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xoá môn học." : result.Error;
        return Go(nameof(Index));
    }

    private async Task Options(CourseViewModel model, CancellationToken ct)
    {
        var result = await catalog.ListActiveUsersInRoleAsync(RoleNames.Lecturer, ct);
        model.Lecturers = (result.Data ?? [])
            .Where(x => !LecturerId.HasValue || x.UserId == LecturerId)
            .Select(x => new SelectListItem(x.FullName, x.UserId.ToString()))
            .ToList();
    }
}
