using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// The question bank, plus the bulk import that fills it. A lecturer is limited to the
/// courses they own, which <see cref="QuestionService"/> enforces; an admin passes a null
/// lecturer id and sees everything.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class QuestionsController(
    IQuestionService questionService,
    ICourseMaterialService materialService,
    ICatalogService catalog) : Controller
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    [HttpGet]
    public async Task<IActionResult> Index(
        int? courseId,
        int? materialId,
        QuestionDifficulty? difficulty,
        BloomLevel? bloomLevel,
        string? term,
        bool includeArchived,
        CancellationToken ct)
    {
        return View(await BuildIndexModelAsync(courseId, materialId, difficulty, bloomLevel, term, includeArchived, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Snapshot(
        int? courseId,
        int? materialId,
        QuestionDifficulty? difficulty,
        BloomLevel? bloomLevel,
        string? term,
        bool includeArchived,
        CancellationToken ct)
    {
        return PartialView("_IndexContent", await BuildIndexModelAsync(
            courseId, materialId, difficulty, bloomLevel, term, includeArchived, ct));
    }

    private async Task<QuestionListViewModel> BuildIndexModelAsync(
        int? courseId,
        int? materialId,
        QuestionDifficulty? difficulty,
        BloomLevel? bloomLevel,
        string? term,
        bool includeArchived,
        CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var selectedCourseId = NormaliseCourse(courseId, allowed);

        var result = await questionService.ListAsync(
            LecturerId, selectedCourseId, materialId, difficulty, bloomLevel, term, includeArchived, ct);

        var model = new QuestionListViewModel
        {
            Questions = result.Data ?? [],
            CourseOptions = ToCourseOptions(courses),
            MaterialOptions = await MaterialOptionsAsync(selectedCourseId, ct),
            DifficultyOptions = DifficultySelectList(),
            BloomOptions = BloomSelectList(),
            CourseId = selectedCourseId,
            MaterialId = materialId,
            Difficulty = difficulty,
            BloomLevel = bloomLevel,
            Term = term,
            IncludeArchived = includeArchived,
            LoadError = result.Success ? null : result.Error
        };

        return model;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? courseId, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var model = new QuestionEditViewModel
        {
            CourseId = NormaliseCourse(courseId, allowed) ?? allowed.FirstOrDefault()?.CourseId ?? 0,
            Options = QuestionEditViewModel.BuildOptions()
        };
        await OptionsAsync(model, ct);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var result = await questionService.GetAsync(id, LecturerId, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        var detail = result.Data;
        var model = new QuestionEditViewModel
        {
            QuestionId = detail.QuestionId,
            CourseId = detail.CourseId,
            MaterialId = detail.MaterialId,
            QuestionText = detail.QuestionText,
            ExpectedAnswer = detail.ExpectedAnswer,
            Difficulty = detail.Difficulty,
            BloomLevel = detail.BloomLevel,
            QuestionType = detail.QuestionType,
            Options = detail.Options.Count > 0
                ? detail.Options
                    .Select(option => new QuestionOptionViewModel { Text = option.Text })
                    .ToList()
                : QuestionEditViewModel.BuildOptions(),
            CorrectIndex = detail.Options
                .Select((option, index) => option.IsCorrect ? index : -1)
                .FirstOrDefault(index => index >= 0, -1)
        };

        await OptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(QuestionEditViewModel model, CancellationToken ct)
    {
        // A lecturer can never move a question into another course: the field is bound for
        // the form but dropped from the posted values.
        if (LecturerId.HasValue)
        {
            ModelState.Remove(nameof(model.CourseId));
        }

        if (ModelState.IsValid)
        {
            var result = await questionService.SaveAsync(
                new QuestionSaveRequest
                {
                    QuestionId = model.QuestionId,
                    CourseId = model.CourseId,
                    MaterialId = model.MaterialId,
                    QuestionText = model.QuestionText,
                    ExpectedAnswer = model.ExpectedAnswer,
                    Difficulty = model.Difficulty,
                    BloomLevel = model.BloomLevel,
                    QuestionType = model.QuestionType,
                    Options = model.Options
                        .Select((option, index) => new QuestionOptionInput
                        {
                            Text = option.Text,
                            IsCorrect = index == model.CorrectIndex
                        })
                        .Where(option => !string.IsNullOrWhiteSpace(option.Text))
                        .ToList()
                },
                CurrentUserId,
                LecturerId,
                ct);

            if (result.Success)
            {
                TempData["Success"] = model.QuestionId == 0
                    ? "Đã thêm câu hỏi vào ngân hàng."
                    : "Đã cập nhật câu hỏi.";
                return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await OptionsAsync(model, ct);
        return View("Edit", model);
    }

    /// <summary>
    /// Retires a question. If any exam already holds it the bank archives it instead of
    /// deleting, so past exams stay readable.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken ct)
    {
        var result = await questionService.ArchiveAsync(id, LecturerId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã ẩn câu hỏi khỏi ngân hàng."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Import(int? courseId, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var model = new QuestionImportViewModel
        {
            CourseId = NormaliseCourse(courseId, allowed) ?? allowed.FirstOrDefault()?.CourseId ?? 0
        };
        await ImportOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(CsvImportRules.MaxBytes + (1024 * 1024))]
    public async Task<IActionResult> Import(QuestionImportViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue)
        {
            ModelState.Remove(nameof(model.CourseId));
        }

        if (ModelState.IsValid)
        {
            var file = model.File!;
            if (!CsvImportRules.ResolveCsv(file.FileName))
            {
                ModelState.AddModelError(nameof(model.File), "Vui lòng chọn file .csv.");
            }
            else if (file.Length > CsvImportRules.MaxBytes)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    $"File vượt quá {CsvImportRules.MaxBytes / (1024 * 1024)} MB.");
            }
            else
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var content = await reader.ReadToEndAsync(ct);

                var materialList = await materialService.ListAsync(LecturerId, model.CourseId, ct);
                var materials = materialList.Data ?? [];
                var materialIds = materials.ToDictionary(item => item.FileName, item => item.MaterialId);

                var result = await questionService.ImportCsvAsync(
                    model.CourseId, content, materialIds, CurrentUserId, LecturerId, ct);

                if (!result.Success)
                {
                    ModelState.AddModelError(string.Empty, result.Error!);
                }
                else
                {
                    model.Result = result.Data is null
                        ? null
                        : new QuestionImportResult
                        {
                            Imported = result.Data.Imported,
                            Errors = result.Data.Errors
                        };

                    foreach (var message in result.Data?.Errors ?? [])
                    {
                        ModelState.AddModelError(string.Empty, message);
                    }
                }

                // Only a clean file leaves the screen; anything skipped comes back with
                // the count so the lecturer can fix the source and retry.
                if (result.Success
                    && result.Data is not null
                    && result.Data.Errors.Count == 0)
                {
                    TempData["Success"] = $"Đã nhập {result.Data.Imported} câu hỏi.";
                    return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
                }
            }
        }

        await ImportOptionsAsync(model, ct);
        return View(model);
    }

    /// <summary>Feeds the exam scheduler: the CSV template the import screen expects.</summary>
    [HttpGet]
    public IActionResult Template() => File(
        CsvImportRules.Content,
        "text/csv; charset=utf-8",
        "mau-nhap-cau-hoi.csv");

    private async Task OptionsAsync(QuestionEditViewModel model, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        model.CourseOptions = ToCourseOptions(courses);
        model.MaterialOptions = await MaterialOptionsAsync(
            model.CourseId > 0 ? model.CourseId : null, ct);
        model.DifficultyOptions = DifficultySelectList();
        model.BloomOptions = BloomSelectList();
        model.QuestionTypeOptions = QuestionTypeSelectList();
    }

    private async Task ImportOptionsAsync(QuestionImportViewModel model, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        model.CourseOptions = ToCourseOptions(courses);
    }

    /// <summary>Topics of one course, or every topic the caller may see.</summary>
    private async Task<IReadOnlyList<SelectListItem>> MaterialOptionsAsync(
        int? courseId,
        CancellationToken ct)
    {
        var result = await materialService.ListAsync(LecturerId, courseId, ct);
        if (!result.Success || result.Data is null)
        {
            return [];
        }

        return result.Data
            .OrderBy(item => item.FileName)
            .Select(item => new SelectListItem($"{item.FileName} ({item.QuestionCount})", item.MaterialId.ToString()))
            .ToList();
    }

    private async Task<IReadOnlyList<CourseResponse>> ActiveCoursesAsync(CancellationToken ct)
    {
        var result = await catalog.ListActiveCoursesAsync(ct);
        return result.Data ?? [];
    }

    private IReadOnlyList<SelectListItem> ToCourseOptions(IReadOnlyList<CourseResponse> courses) => CoursesFor(courses, LecturerId)
        .Select(course => new SelectListItem($"{course.CourseCode} · {course.CourseName}", course.CourseId.ToString()))
        .ToList();

    private IReadOnlyList<CourseResponse> CoursesFor(IReadOnlyList<CourseResponse> courses, int? lecturerId) => courses
        .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
        .OrderBy(course => course.CourseCode)
        .ToList();

    /// <summary>Keeps a course filter the caller is not allowed to see from falling through.</summary>
    private static int? NormaliseCourse(int? courseId, IReadOnlyList<CourseResponse> allowed) =>
        courseId is int requested && allowed.Any(course => course.CourseId == requested)
            ? requested
            : null;
    private static IReadOnlyList<SelectListItem> DifficultySelectList() =>
        Enum.GetValues<QuestionDifficulty>()
            .Select(value => new SelectListItem(QuestionText.Difficulty(value), value.ToString()))
            .ToList();

    private static IReadOnlyList<SelectListItem> BloomSelectList() =>
        Enum.GetValues<BloomLevel>()
            .Select(value => new SelectListItem(QuestionText.Bloom(value), value.ToString()))
            .ToList();

    private static IReadOnlyList<SelectListItem> QuestionTypeSelectList() =>
        Enum.GetValues<QuestionType>()
            .Select(value => new SelectListItem(QuestionText.Type(value), value.ToString()))
            .ToList();
}
