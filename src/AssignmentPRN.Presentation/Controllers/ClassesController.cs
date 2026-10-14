using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.Models;
using AssignmentPRN.Presentation.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public sealed class ClassesController(
    IClassManagementService classService,
    IExamSessionService examSessionService,
    IHubContext<ExamUpdatesHub> updatesHub) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var response = await classService.ListAsync(cancellationToken);
        var classes = response.Data ?? Array.Empty<AcademicClassResponse>();
        if (CurrentRole == RoleNames.Lecturer)
        {
            classes = classes.Where(item => item.LecturerId == CurrentUserId).ToList();
        }

        var query = q?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(query))
        {
            classes = classes.Where(item =>
                    item.ClassCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.CourseCode.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return View(new ClassListViewModel
        {
            Classes = classes,
            Query = query,
            Error = response.Success ? null : response.Error
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? q, CancellationToken cancellationToken)
    {
        var classInfo = await FindAuthorizedClassAsync(id, cancellationToken);
        if (classInfo is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var roster = await classService.GetAsync(id, cancellationToken);
        if (!roster.Success || roster.Data is null)
        {
            TempData["Error"] = roster.Error;
            return RedirectToAction(nameof(Index));
        }

        var query = q?.Trim() ?? string.Empty;
        IReadOnlyList<PersonResponse> matches = Array.Empty<PersonResponse>();
        if (!string.IsNullOrEmpty(query))
        {
            var search = await examSessionService.SearchStudentsAsync(query, cancellationToken);
            if (search.Success && search.Data is not null)
            {
                var enrolledIds = roster.Data.Students.Select(item => item.UserId).ToHashSet();
                matches = search.Data.Where(item => !enrolledIds.Contains(item.UserId)).ToList();
            }
        }

        return View(new ClassDetailViewModel
        {
            Class = classInfo,
            Roster = roster.Data,
            Query = query,
            Matches = matches
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        if (await FindAuthorizedClassAsync(id, cancellationToken) is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var response = await classService.AddStudentAsync(id, studentId, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã thêm sinh viên vào lớp."
            : response.Error;
        if (response.Success)
        {
            await BroadcastAsync(id, "Danh sách sinh viên của lớp vừa thay đổi.", cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStudent(int id, int studentId, CancellationToken cancellationToken)
    {
        if (await FindAuthorizedClassAsync(id, cancellationToken) is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var response = await classService.RemoveStudentAsync(id, studentId, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xoá sinh viên khỏi lớp."
            : response.Error;
        if (response.Success)
        {
            await BroadcastAsync(id, "Danh sách sinh viên của lớp vừa thay đổi.", cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<AcademicClassResponse?> FindAuthorizedClassAsync(
        int classId,
        CancellationToken cancellationToken)
    {
        var response = await classService.ListAsync(cancellationToken);
        var item = response.Data?.FirstOrDefault(value => value.ClassId == classId);
        return item is not null && RoleAccessRules.CanManageClass(CurrentRole, CurrentUserId, item.LecturerId)
            ? item
            : null;
    }

    private Task BroadcastAsync(int classId, string message, CancellationToken cancellationToken) =>
        updatesHub.Clients.All.SendAsync(
            "ExamListChanged",
            new { change = "class-roster", classId, message },
            cancellationToken);

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    private string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);
}
