using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using AssignmentPRN.Presentation.Hubs;
using AssignmentPRN.Presentation.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Pages.ExamSessions;

[StaffPage]
public abstract class ExamSessionsPage(
    IExamSessionService examSessionService,
    IQuestionService questionService,
    ICourseMaterialService materialService, IExamRealtimeNotifier updates) : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    public object Data { get; private set; } = null!;
    protected IActionResult View(object model) { Data = model; return Page(); }
    protected IActionResult View(string name, object model) => View(model);
    protected IActionResult Go(string action, object? values = null) => RedirectToPage("/ExamSessions/" + action, values);
    protected JsonResult Json(object value) => new(value);

    private int? CurrentLecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    private bool CanManage(ExamSessionDetailResponse session) => !CurrentLecturerId.HasValue || session.Lecturer.UserId == CurrentLecturerId;

    /// <summary>
    /// Sends a lecturer who acted on someone else's session back to the schedule with the
    /// usual error banner. <see cref="ControllerBase.Forbid()"/> cannot be used: the app signs
    /// people in through the session, so no authentication scheme exists to forbid with.
    /// </summary>
    private IActionResult DenySession()
    {
        TempData["Error"] = "Bạn không có quyền thao tác phiên thi này.";
        return Go(nameof(Index));
    }

    protected async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await examSessionService.GetAsync(id, cancellationToken);
        if (result.Data is null) return NotFound();
        if (!CanManage(result.Data)) return StatusCode(403);
        var item = result.Data;
        if (!ExamSessionRules.CanEdit(item.Status))
        {
            TempData["Error"] = "Phiên thi không còn được phép chỉnh sửa.";
            return Go(nameof(Details), new { id });
        }
        var model = new ExamSessionEditViewModel { ExamId = id, CourseId = item.Course.CourseId,
            ExamName = item.ExamName, Description = item.Description, StartTime = item.StartTime,
            TimePerStudent = item.TimePerStudent, MainQuestionCount = item.MainQuestionCount, MaxFollowUpCount = item.MaxFollowUpCount };
        await EditOptions(model, cancellationToken);
        return View(model);
    }

    protected async Task<IActionResult> Edit(int id, ExamSessionEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.ExamId) return BadRequest();
        var current = await examSessionService.GetAsync(id, cancellationToken);
        if (current.Data is null) return NotFound();
        if (!CanManage(current.Data)) return StatusCode(403);
        if (ModelState.IsValid)
        {
            var result = await examSessionService.UpdateAsync(new ExamSessionUpdateInput {
                ExamId = id, CourseId = model.CourseId, ExamName = model.ExamName, Description = model.Description,
                StartTime = model.StartTime!.Value, TimePerStudent = model.TimePerStudent,
                MainQuestionCount = model.MainQuestionCount, MaxFollowUpCount = model.MaxFollowUpCount
            }, CurrentLecturerId, cancellationToken);
            if (result.Success)
            {
                TempData["Success"] = "Đã cập nhật phiên thi.";
                await BroadcastAsync("updated", id, "", cancellationToken);
                return Go(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await EditOptions(model, cancellationToken);
        return View(model);
    }

    protected async Task<IActionResult> ChangeStatus(int id, ExamSessionStatus status, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await examSessionService.ChangeStatusAsync(id, status, CurrentLecturerId, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã cập nhật trạng thái phiên thi." : result.Error;
        if (result.Success) await BroadcastAsync("status", id, "", cancellationToken);
        return Go(nameof(Details), new { id });
    }

    private async Task EditOptions(ExamSessionEditViewModel model, CancellationToken cancellationToken)
    {
        var options = await examSessionService.GetCreationOptionsAsync(cancellationToken);
        model.Courses = options.Data?.Courses.Select(x => new SelectListItem(x.Label, x.Id.ToString())).ToList() ?? [];
        var current = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (current.Data is not null && CanManage(current.Data) && !model.Courses.Any(x => x.Value == current.Data.Course.CourseId.ToString()))
            model.Courses.Add(new SelectListItem($"{current.Data.Course.CourseCode} · {current.Data.Course.CourseName} (ngừng hoạt động)", current.Data.Course.CourseId.ToString()));
        if (!options.Success) ModelState.AddModelError(string.Empty, options.Error!);
    }

    protected async Task<IActionResult> RemoveStudent(int examId, int candidateId,
        CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(examId, cancellationToken);
        if (!session.Success || session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return DenySession();

        var response = await examSessionService.RemoveStudentAsync(examId, candidateId, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xóa sinh viên khỏi phiên thi. Tài khoản sinh viên vẫn được giữ nguyên." : response.Error;
        if (response.Success) await updates.ChangedAsync(new(examId, session.Data.Lecturer.UserId, session.Data.Candidates.Select(x => x.StudentId).ToArray(), "roster"), cancellationToken);
        return Go(nameof(Details), new { id = examId });
    }

    protected async Task<IActionResult> AddStudent(ExamSessionAddStudentViewModel model,
        CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (!session.Success || session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return DenySession();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập email sinh viên và khung giờ hợp lệ.";
            return Go(nameof(Details), new { id = model.ExamId });
        }
        var response = await examSessionService.AddStudentAsync(model.ExamId,
            model.Email, model.ScheduledTime!.Value, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã thêm sinh viên vào phiên thi." : response.Error;
        if (response.Success) await BroadcastAsync("updated", model.ExamId, "", cancellationToken);
        return Go(nameof(Details), new { id = model.ExamId });
    }

    protected async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var response = await examSessionService.ListAsync(cancellationToken);

        var sessions = response.Data ?? Array.Empty<ExamSessionListItemResponse>();
        if (CurrentRole == RoleNames.Lecturer)
        {
            sessions = sessions.Where(item => item.LecturerId == CurrentUserId).ToList();
        }

        return View(new ExamSessionListViewModel
        {
            Sessions = response.Data?.Where(x => !CurrentLecturerId.HasValue || x.LecturerId == CurrentLecturerId).ToList()
                ?? new List<ExamSessionListItemResponse>(),
            LoadError = response.Success ? null : response.Error
        });
    }

    protected async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            TempData["Error"] = response.Error;
            return Go(nameof(Index));
        }

        if (!CanManage(response.Data)) return StatusCode(403);
        return View(new ExamSessionDetailViewModel { Session = response.Data });
    }

    protected async Task<IActionResult> Create(CancellationToken cancellationToken)
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

    protected async Task<IActionResult> Create(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
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

        // No draw happens here. Each student's paper is dealt when they open their slot,
        // so a session is never blocked on the lecturer remembering to hand questions out.
        TempData["Success"] = "Đã tạo lịch thi và sinh khung giờ tự động. "
            + "Đề sẽ được rút ngẫu nhiên khi từng sinh viên vào thi.";
        await BroadcastAsync("created", response.Data.ExamId, "Lịch thi mới vừa được tạo.", cancellationToken);
        return Go(nameof(Details), new { id = response.Data.ExamId });
    }

    protected async Task<IActionResult> Reschedule(
        ExamSessionRescheduleViewModel model,
        CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);
        if (!session.Data.Candidates.Any(x => x.CandidateId == model.CandidateId)) return BadRequest();
        if (!ModelState.IsValid || !model.ScheduledTime.HasValue)
        {
            TempData["Error"] = "Khung giờ mới không hợp lệ.";
            return Go(nameof(Details), new { id = model.ExamId });
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

        return Go(nameof(Details), new { id = model.ExamId });
    }

    protected async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(id, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);
        var response = await examSessionService.DeleteAsync(id, cancellationToken);
        TempData[response.Success ? "Success" : "Error"] = response.Success
            ? "Đã xoá lịch thi."
            : response.Error;

        if (response.Success)
        {
            await updates.ChangedAsync(new(id, session.Data.Lecturer.UserId, session.Data.Candidates.Select(x => x.StudentId).ToArray(), "deleted"), cancellationToken);
        }

        return Go(nameof(Index));
    }

    /// <summary>
    /// Feeds the student combobox. Returns at most a page of matches so the
    /// browser never has to hold the whole student list.
    /// </summary>
    protected async Task<IActionResult> SearchStudents(string? q, CancellationToken cancellationToken)
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

    protected async Task<IActionResult> ClassRoster(int classId, CancellationToken cancellationToken)
    {
        var response = await examSessionService.GetClassRosterAsync(classId, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(new { error = response.Error });
        }

        if (CurrentLecturerId.HasValue && response.Data.LecturerId != CurrentLecturerId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bạn không có quyền xem lớp học này." });
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

    /// <summary>
    /// The question-bank configuration of one exam: how many questions per student and
    /// which topics/difficulties they are drawn from, plus the papers already dealt.
    /// </summary>
    protected async Task<IActionResult> Questions(int id, CancellationToken cancellationToken,
        List<int>? materialIds = null, List<QuestionDifficulty>? difficulties = null, bool preview = false)
    {
        var session = await examSessionService.GetAsync(id, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);

        var model = new ExamQuestionAssignViewModel
        {
            ExamId = id,
            CountPerCandidate = session.Data.MainQuestionCount
        };

        var config = await questionService.GetExamConfigurationAsync(id, cancellationToken);
        if (!config.Success || config.Data is null) {
            TempData["Error"] = config.Error;
            return Go(nameof(Details), new { id });
        }
        model.MaterialIds = config.Data.MaterialIds.ToList();
        model.Difficulties = config.Data.Difficulties.ToList();
        if (preview)
        {
            model.MaterialIds = materialIds ?? [];
            model.Difficulties = difficulties ?? [];
        }

        await LoadQuestionConfigAsync(model, session.Data, cancellationToken);
        return View(model);
    }

    protected async Task<IActionResult> AssignQuestions(
        ExamQuestionAssignViewModel model,
        CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(model.ExamId, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);
        if (!ExamSessionRules.CanEdit(session.Data.Status))
        {
            TempData["Error"] = "Phiên thi không còn được phép phát đề.";
            return Go(nameof(Details), new { id = model.ExamId });
        }

        if (ModelState.IsValid)
        {
            var result = await questionService.AssignToExamAsync(
                new ExamQuestionAssignmentRequest
                {
                    ExamId = model.ExamId,
                    CourseId = session.Data.Course.CourseId,
                    CountPerCandidate = model.CountPerCandidate,
                    MaterialIds = model.MaterialIds,
                    Difficulties = model.Difficulties
                },
                cancellationToken);

            if (result.Success && result.Data is not null)
            {
                TempData["Success"] = $"Đã phát {result.Data.AssignedCount} câu hỏi cho "
                    + $"{result.Data.CandidateCount} sinh viên, không sinh viên nào trùng câu.";
                return Go(nameof(Questions), new { id = model.ExamId });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await LoadQuestionConfigAsync(model, session.Data, cancellationToken);
        return View(nameof(Questions), model);
    }

    protected async Task<IActionResult> ClearQuestions(int id, CancellationToken cancellationToken)
    {
        var session = await examSessionService.GetAsync(id, cancellationToken);
        if (session.Data is null) return NotFound();
        if (!CanManage(session.Data)) return StatusCode(403);

        var result = await questionService.ClearExamAssignmentAsync(id, cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã huỷ đề đã phát. Có thể phát lại với phạm vi khác."
            : result.Error;

        return Go(nameof(Questions), new { id });
    }

    /// <summary>
    /// Fills the config screen: topics of the course, the papers already dealt and how many
    /// questions the filter currently on screen can still supply.
    /// </summary>
    private async Task LoadQuestionConfigAsync(
        ExamQuestionAssignViewModel model,
        ExamSessionDetailResponse session,
        CancellationToken cancellationToken)
    {
        model.ExamName = session.ExamName;
        model.CourseId = session.Course.CourseId;
        model.CourseCode = session.Course.CourseCode;
        model.CourseName = session.Course.CourseName;
        model.CanAssign = ExamSessionRules.CanEdit(session.Status);

        var materials = await materialService.ListAsync(
            CurrentLecturerId, session.Course.CourseId, cancellationToken);
        model.MaterialOptions = materials.Data?
            .OrderBy(item => item.FileName)
            .Select(item => new SelectListItem(
                $"{item.FileName} ({item.QuestionCount} câu)", item.MaterialId.ToString()))
            .ToList() ?? [];

        model.DifficultyOptions = Enum.GetValues<QuestionDifficulty>()
            .Select(value => new SelectListItem(QuestionText.Difficulty(value), value.ToString()))
            .ToList();

        var papers = await questionService.GetExamPaperAsync(session.ExamId, cancellationToken);
        model.Papers = papers.Data ?? [];

        // Once a student has opened their slot the papers are part of the exam record, so
        // the redeal button disappears, matching what the service would refuse.
        model.CanRedeal = model.Papers.Any(paper => paper.Questions.Count > 0)
            && model.CanAssign
            && model.Papers.All(paper => !paper.HasStarted
                && paper.Questions.All(question => !question.IsCompleted));

        // Ask the bank with the filter currently on screen, so the warning matches what the
        // button would actually draw from. Once everybody holds a paper nothing more is
        // needed, but the bank still rejects a count of zero — ask for one and read only
        // the number available.
        var availability = await questionService.CheckAvailabilityAsync(
            new QuestionPickRequest
            {
                CourseId = session.Course.CourseId,
                Count = Math.Max(model.RequiredCount, 1),
                MaterialIds = model.MaterialIds,
                Difficulties = model.Difficulties,
                TakenQuestionIds = model.Papers
                    .SelectMany(paper => paper.Questions)
                    .Select(question => question.QuestionId)
                    .Distinct()
                    .ToList()
            },
            cancellationToken);

        model.AvailableCount = availability.Data?.Available ?? 0;
        model.LoadError = materials.Success && papers.Success && availability.Success
            ? null
            : materials.Error ?? papers.Error ?? availability.Error;
    }

    private async Task LoadOptionsAsync(ExamSessionCreateViewModel model, CancellationToken cancellationToken)
    {
        model.IsLecturerFixed = CurrentLecturerId.HasValue;
        if (CurrentLecturerId is int currentLecturerId)
        {
            model.LecturerId = currentLecturerId;
            ModelState.Remove(nameof(model.LecturerId));
        }
        var response = await examSessionService.GetCreationOptionsAsync(cancellationToken);
        if (!response.Success || response.Data is null)
        {
            model.OptionsError = response.Error;
            return;
        }

        model.CourseOptions = ToSelectList(response.Data.Courses);
        model.LecturerOptions = ToSelectList(response.Data.Lecturers
            .Where(item => !CurrentLecturerId.HasValue || item.Id == CurrentLecturerId.Value).ToList());
        var classOptions = response.Data.Classes.AsEnumerable();
        if (HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer)
        {
            var lecturerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
            classOptions = classOptions.Where(item => item.LecturerId == lecturerId);
            model.LecturerOptions = model.LecturerOptions
                .Where(item => item.Value == lecturerId.ToString())
                .ToList();
        }
        // The course each class belongs to rides along in Group, so the class
        // picker can hide the classes of every other course.
        model.ClassOptions = classOptions
            .Select(item => new SelectListItem(item.Label, item.ClassId.ToString())
            {
                Group = new SelectListGroup { Name = item.CourseId.ToString() }
            })
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

    private async Task BroadcastAsync(string change, int examId, string message, CancellationToken ct)
    {
        var response = await examSessionService.GetAsync(examId, ct);
        if (response.Data is { } session)
            await updates.ChangedAsync(new(examId, session.Lecturer.UserId, session.Candidates.Select(x => x.StudentId).ToArray(), change), ct);
    }
}
