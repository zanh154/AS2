using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.Models;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Controllers;

[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class ExamSessionsController(
    IExamSessionService examSessionService,
    IHubContext<ExamUpdatesHub> updatesHub) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await examSessionService.ListAsync(cancellationToken);

        var sessions = response.Data ?? Array.Empty<ExamSessionListItemResponse>();
        if (CurrentRole == RoleNames.Lecturer)
        {
            sessions = sessions.Where(item => item.LecturerId == CurrentUserId).ToList();
        }

        return View(new ExamSessionListViewModel
        {
            Sessions = sessions,
            LoadError = response.Success ? null : response.Error
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            TempData["Error"] = response.Error;
            return RedirectToAction(nameof(Index));
        }

        if (!CanManage(response.Data.Lecturer.UserId))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        return View(new ExamSessionDetailViewModel { Session = response.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ExamSessionCreateViewModel();

        // A lecturer schedules their own exams, so preselect their account.
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            model.LecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        }

        await LoadOptionsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
    {
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            model.LecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var response = await examSessionService.CreateAsync(
            new ExamSessionCreateRequest
            {
                CourseId = model.CourseId,
                LecturerId = model.LecturerId,
                ClassId = model.ClassId,
                ExamName = model.ExamName,
                Description = model.Description,
                StartTime = model.StartTime!.Value,
                TimePerStudent = model.TimePerStudent,
                MainQuestionCount = model.MainQuestionCount,
                MaxFollowUpCount = model.MaxFollowUpCount
            },
            cancellationToken);

        if (!response.Success || response.Data is null)
        {
            AddErrors(response.Errors, response.Error);
            await LoadOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Đã tạo lịch thi và sinh khung giờ tự động.";
        await BroadcastAsync("created", response.Data.ExamId, "Lịch thi mới vừa được tạo.", cancellationToken);
        return RedirectToAction(nameof(Details), new { id = response.Data.ExamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(
        ExamSessionRescheduleViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !model.ScheduledTime.HasValue)
        {
            TempData["Error"] = "Khung giờ mới không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.ExamId });
        }

        var existing = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (!existing.Success || existing.Data is null
            || !CanManage(existing.Data.Lecturer.UserId)
            || existing.Data.Candidates.All(item => item.CandidateId != model.CandidateId))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var response = await examSessionService.RescheduleAsync(
            new ExamSessionRescheduleRequest
            {
                CandidateId = model.CandidateId,
                ScheduledTime = model.ScheduledTime.Value
            },
            cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã đổi khung giờ và kiểm tra không trùng lịch."
            : response.Error;

        if (response.Success)
        {
            await BroadcastAsync("rescheduled", model.ExamId, "Một khung giờ thi vừa thay đổi.", cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = model.ExamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await examSessionService.GetAsync(id, cancellationToken);
        if (!existing.Success || existing.Data is null || !CanManage(existing.Data.Lecturer.UserId))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var response = await examSessionService.DeleteAsync(id, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xoá lịch thi."
            : response.Error;

        if (response.Success)
        {
            await BroadcastAsync("deleted", id, "Một lịch thi vừa được xoá.", cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Feeds the student combobox. Returns at most a page of matches so the
    /// browser never has to hold the whole student list.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchStudents(string? q, CancellationToken cancellationToken)
    {
        var response = await examSessionService.SearchStudentsAsync(q ?? string.Empty, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return Json(Array.Empty<object>());
        }

        return Json(response.Data.Select(person => new
        {
            email = person.Email,
            name = person.FullName
        }));
    }

    [HttpGet]
    public async Task<IActionResult> ClassRoster(int classId, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetClassRosterAsync(classId, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(new { error = response.Error });
        }

        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
            && response.Data.LecturerId != HttpContext.Session.GetInt32(SessionKeys.UserId))
        {
            return Forbid();
        }

        return Json(new
        {
            classId = response.Data.ClassId,
            courseId = response.Data.CourseId,
            lecturerId = response.Data.LecturerId,
            classCode = response.Data.ClassCode,
            className = response.Data.ClassName,
            students = response.Data.Students.Select(student => new
            {
                id = student.UserId,
                name = student.FullName,
                email = student.Email
            })
        });
    }

    private async Task LoadOptionsAsync(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetCreationOptionsAsync(cancellationToken);
        if (!response.Success || response.Data is null)
        {
            model.OptionsError = response.Error;
            return;
        }

        model.CourseOptions = ToSelectList(response.Data.Courses);
        model.LecturerOptions = ToSelectList(response.Data.Lecturers);
        var classOptions = response.Data.Classes.AsEnumerable();
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            var lecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
            classOptions = classOptions.Where(item => item.LecturerId == lecturerId);
            model.LecturerOptions = model.LecturerOptions
                .Where(item => item.Value == lecturerId.ToString())
                .ToList();
        }
        model.ClassOptions = classOptions
            .Select(item => new SelectListItem(item.Label, item.ClassId.ToString()))
            .ToList();

        model.OptionsError = model.CanCreate
            ? null
            : "Cần có môn học, giảng viên và lớp học đang hoạt động trước khi tạo lịch thi.";
    }

    private static List<SelectListItem> ToSelectList(IReadOnlyList<LookupOption> options)
    {
        return options
            .Select(option => new SelectListItem(option.Label, option.Id.ToString()))
            .ToList();
    }

    private void AddErrors(IReadOnlyList<string> errors, string? error)
    {
        var values = errors.Count > 0 ? errors : [error ?? "Không thể tạo lịch thi."];
        foreach (var value in values)
        {
            ModelState.AddModelError(string.Empty, value);
        }
    }

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    private string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    private bool CanManage(int lecturerId) =>
        RoleAccessRules.CanManageExam(CurrentRole, CurrentUserId, lecturerId);

    private Task BroadcastAsync(string change, int examId, string message, CancellationToken cancellationToken) =>
        updatesHub.Clients.All.SendAsync(
            "ExamListChanged",
            new { change, examId, message },
            cancellationToken);
}
