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
/// Course materials, which double as the topics a question can be filed under.
/// The controller coordinates metadata and byte storage through abstractions.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class CourseMaterialsController(
    ICourseMaterialService materialService,
    ICatalogService catalog) : Controller
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0 : null;

    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    [HttpGet]
    public async Task<IActionResult> Index(int? courseId, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var selected = courseId is int requested && allowed.Any(course => course.CourseId == requested)
            ? requested
            : (int?)null;

        var result = await materialService.ListAsync(LecturerId, selected, ct);
        var model = new CourseMaterialListViewModel
        {
            Materials = result.Data ?? [],
            CourseOptions = ToCourseOptions(courses),
            CourseId = selected,
            LoadError = result.Success ? null : result.Error
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int? courseId, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var model = new CourseMaterialUploadViewModel
        {
            CourseId = courseId is int requested && allowed.Any(course => course.CourseId == requested)
                ? requested
                : allowed.FirstOrDefault()?.CourseId ?? 0
        };

        await OptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MaterialFileRules.MaxBytes + (1024 * 1024))]
    public async Task<IActionResult> Upload(CourseMaterialUploadViewModel model, CancellationToken ct)
    {
        if (LecturerId.HasValue)
        {
            ModelState.Remove(nameof(model.CourseId));
        }

        if (ModelState.IsValid)
        {
            var file = model.File!;
            var fileType = MaterialFileRules.ResolveType(file.FileName);
            if (fileType is null)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    $"Chỉ nhận {string.Join(", ", MaterialFileRules.AcceptedExtensions)}.");
            }
            else if (file.Length > MaterialFileRules.MaxBytes)
            {
                ModelState.AddModelError(
                    nameof(model.File),
                    $"Tệp vượt quá {MaterialFileRules.MaxBytes / (1024 * 1024)} MB.");
            }
            else
            {
                await using var content = file.OpenReadStream();
                var result = await materialService.UploadAsync(
                    model.CourseId,
                    file.FileName,
                    content,
                    file.Length,
                    fileType.Value,
                    CurrentUserId,
                    ct);

                if (result.Success)
                {
                    TempData["Success"] = "Đã tải lên tài liệu.";
                    return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
                }

                ModelState.AddModelError(string.Empty, result.Error!);
            }
        }

        await OptionsAsync(model, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var material = await materialService.GetAsync(id, ct);
        if (material is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu.";
            return RedirectToAction(nameof(Index));
        }

        // GetAsync is a plain lookup, so ownership is checked here as well as in the
        // service; otherwise a lecturer could open the editor for somebody else's course.
        var courses = await ActiveCoursesAsync(ct);
        if (!CoursesFor(courses, LecturerId).Any(course => course.CourseId == material.CourseId))
        {
            return StatusCode(403);
        }

        var model = new CourseMaterialEditViewModel
        {
            MaterialId = material.MaterialId,
            CourseId = material.CourseId,
            FileName = material.FileName,
            CurrentCourseLabel = $"{material.CourseCode} · {material.CourseName}",
            QuestionCount = material.QuestionCount
        };

        await EditOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseMaterialEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await materialService.UpdateAsync(
                new CourseMaterialUpdateRequest
                {
                    MaterialId = model.MaterialId,
                    CourseId = model.CourseId,
                    FileName = model.FileName
                },
                LecturerId,
                ct);

            if (result.Success && result.Data is not null)
            {
                TempData["Success"] = "Đã cập nhật tài liệu.";
                return RedirectToAction(nameof(Index), new { courseId = result.Data.CourseId });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        // The current course and the question count come from the database, never from
        // the posted form, so a tampered post cannot unlock the course picker.
        var material = await materialService.GetAsync(model.MaterialId, ct);
        if (material is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu.";
            return RedirectToAction(nameof(Index));
        }

        model.CurrentCourseLabel = $"{material.CourseCode} · {material.CourseName}";
        model.QuestionCount = material.QuestionCount;
        await EditOptionsAsync(model, ct);
        return View(model);
    }

    private async Task EditOptionsAsync(CourseMaterialEditViewModel model, CancellationToken ct)
    {
        model.CourseOptions = ToCourseOptions(await ActiveCoursesAsync(ct));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await materialService.DeleteAsync(id, LecturerId, ct);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã xoá tài liệu."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Opens the stored file so the lecturer can check what they uploaded.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var result = await materialService.DownloadAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = "Không tìm thấy tài liệu.";
            return RedirectToAction(nameof(Index));
        }

        var stream = result.Data.Content;
        if (stream is null)
        {
            TempData["Error"] = "Tệp không còn trên máy chủ.";
            return RedirectToAction(nameof(Index));
        }

        return File(
            stream,
            ContentTypeFor(result.Data.FileType),
            result.Data.FileName,
            enableRangeProcessing: true);
    }

    private async Task OptionsAsync(CourseMaterialUploadViewModel model, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        model.CourseOptions = ToCourseOptions(courses);
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

    private static string ContentTypeFor(MaterialFileType type) => type switch
    {
        MaterialFileType.PDF => "application/pdf",
        MaterialFileType.DOCX =>
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        MaterialFileType.PPTX =>
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}
