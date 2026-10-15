using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using AssignmentPRN.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Controllers;

/// <summary>
/// RAG documents: upload, background chunk/embedding, and AI question generation.
/// MVC front over <see cref="IDocumentService"/>; every rule (course ownership,
/// file limits, review decisions) lives in Business, never in this controller.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class DocumentsController(
    IDocumentService documentService,
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
        var selected = courseId is int requested && allowed.Any(c => c.CourseId == requested)
            ? requested
            : allowed.FirstOrDefault()?.CourseId;

        IReadOnlyList<DocumentResponse> documents = [];
        string? loadError = null;
        if (selected.HasValue)
        {
            var result = await documentService.ListByCourseAsync(selected.Value, ct);
            if (result.Success && result.Data is not null)
            {
                documents = result.Data;
            }
            else
            {
                loadError = result.Error;
            }
        }
        else
        {
            loadError = "Chưa có môn học phù hợp hoặc bạn không có quyền truy cập.";
        }

        return View(new DocumentListViewModel
        {
            Documents = documents,
            CourseOptions = ToCourseOptions(courses),
            CourseId = selected,
            LoadError = loadError
        });
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int? courseId, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        var selected = courseId is int requested && allowed.Any(c => c.CourseId == requested)
            ? requested
            : allowed.FirstOrDefault()?.CourseId ?? 0;

        var model = new DocumentUploadViewModel { CourseId = selected };
        await UploadOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        var allowed = CoursesFor(courses, LecturerId);
        if (!allowed.Any(c => c.CourseId == model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "Bạn không có quyền tải lên môn học này.");
        }

        if (model.File is null || model.File.Length == 0)
        {
            ModelState.AddModelError(nameof(model.File), "Vui lòng chọn tệp tài liệu.");
        }

        if (ModelState.IsValid)
        {
            var file = model.File!;
            await using var content = file.OpenReadStream();
            var result = await documentService.UploadAsync(
                model.CourseId,
                file.FileName,
                content,
                file.Length,
                file.ContentType,
                CurrentUserId,
                ct);

            if (result.Success)
            {
                TempData["Success"] = "Đã tải lên tài liệu. Hệ thống đang xử lý trong nền.";
                return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await UploadOptionsAsync(model, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var result = await documentService.GetAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        IReadOnlyList<GeneratedQuestionResponse> questions = [];
        if (result.Data.Status == DocumentStatus.Completed)
        {
            var listed = await documentService.ListQuestionsByDocumentAsync(id, ct);
            if (listed.Success && listed.Data is not null)
            {
                questions = listed.Data;
            }
        }

        return View(new DocumentDetailsViewModel
        {
            Document = result.Data,
            Questions = questions,
            LoadError = result.Success ? null : result.Error
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int? courseId, CancellationToken ct)
    {
        var result = await documentService.DeleteAsync(id, CurrentUserId, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "Đã xoá tài liệu."
            : result.Error;
        return RedirectToAction(nameof(Index), new { courseId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQuestions(
        int documentId,
        int count,
        int? bloomLevel,
        string? topic,
        CancellationToken ct)
    {
        var request = new GenerateQuestionsRequest(
            Count: count,
            BloomLevel: bloomLevel.HasValue && Enum.IsDefined(typeof(DocumentBloomLevel), bloomLevel.Value)
                ? (DocumentBloomLevel)bloomLevel.Value
                : null,
            Topic: string.IsNullOrWhiteSpace(topic) ? null : topic.Trim());

        var result = await documentService.GenerateQuestionsAsync(documentId, request, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? $"Đã sinh {result.Data?.Count ?? 0} câu hỏi mới."
            : result.Error;
        return RedirectToAction(nameof(Details), new { id = documentId });
    }

    [HttpGet]
    public async Task<IActionResult> Questions(int documentId, int? status, int? bloomLevel, CancellationToken ct)
    {
        var result = await documentService.ListQuestionsByDocumentAsync(documentId, ct);
        var questions = result.Data ?? [];
        if (status.HasValue && Enum.IsDefined(typeof(GeneratedQuestionStatus), status.Value))
        {
            var wanted = (GeneratedQuestionStatus)status.Value;
            questions = questions.Where(q => q.Status == wanted).ToList();
        }
        if (bloomLevel.HasValue && Enum.IsDefined(typeof(DocumentBloomLevel), bloomLevel.Value))
        {
            var wanted = (DocumentBloomLevel)bloomLevel.Value;
            questions = questions.Where(q => q.BloomLevel == wanted).ToList();
        }

        return View(new DocumentQuestionsViewModel
        {
            DocumentId = documentId,
            Questions = questions,
            StatusFilter = status,
            BloomFilter = bloomLevel,
            LoadError = result.Success ? null : result.Error
        });
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id, int documentId, CancellationToken ct)
    {
        var result = await documentService.GetQuestionAsync(id, ct);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Questions), new { documentId });
        }

        return View(new DocumentQuestionReviewViewModel
        {
            DocumentId = documentId,
            Question = result.Data,
            LoadError = result.Success ? null : result.Error
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        int questionId,
        int documentId,
        string questionText,
        string? answerKey,
        string? explanation,
        int bloomLevel,
        string? topic,
        int decision,
        string? notes,
        CancellationToken ct)
    {
        if (decision is not (1 or 2))
        {
            return BadRequest();
        }

        var request = new ReviewQuestionRequest(
            Status: decision == 1 ? GeneratedQuestionStatus.Approved : GeneratedQuestionStatus.Rejected,
            Notes: string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            QuestionText: questionText,
            AnswerKey: answerKey,
            Explanation: explanation,
            BloomLevel: Enum.IsDefined(typeof(DocumentBloomLevel), bloomLevel)
                ? (DocumentBloomLevel)bloomLevel
                : null,
            Topic: string.IsNullOrWhiteSpace(topic) ? null : topic.Trim());

        var result = await documentService.ReviewQuestionAsync(questionId, CurrentUserId, request, ct);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? decision == 1 ? "Đã duyệt câu hỏi." : "Đã từ chối câu hỏi."
            : result.Error;
        return RedirectToAction(nameof(Questions), new { documentId });
    }

    private async Task UploadOptionsAsync(DocumentUploadViewModel model, CancellationToken ct)
    {
        var courses = await ActiveCoursesAsync(ct);
        model.CourseOptions = ToCourseOptions(courses);
        var found = CoursesFor(courses, LecturerId).FirstOrDefault(c => c.CourseId == model.CourseId);
        model.CourseLabel = found is null ? string.Empty : $"{found.CourseCode} - {found.CourseName}";
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
}
