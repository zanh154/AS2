using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Documents.Questions;

[StaffPage]
public class ReviewModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ICourseMaterialService _materials;

    public ReviewModel(IDocumentService documentService, ICourseMaterialService materials)
    {
        _documentService = documentService;
        _materials = materials;
    }

    /// <summary>Course materials the approved question can be filed under as its bank topic.</summary>
    public IReadOnlyList<SelectListItem> MaterialOptions { get; private set; } = [];
    public int? MaterialId { get; set; }

    /// <summary>True when the question came from a course material, which is then its default topic.</summary>
    public bool HasSourceMaterial { get; private set; }

    public int DocumentId { get; set; }
    public GeneratedQuestionResponse? Question { get; set; }
    public bool IsNew => Question?.Status == GeneratedQuestionStatus.PendingReview;
    public string? Error { get; set; }
    public string? Success { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int documentId, CancellationToken cancellationToken)
    {
        DocumentId = documentId;

        var response = await _documentService.GetQuestionAsync(id, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            Error = response.Error;
            return Page();
        }

        Question = response.Data;
        DocumentId = Question.DocumentId;
        await LoadMaterialOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        int questionId,
        int documentId,
        string questionText,
        string? answerKey,
        string? explanation,
        int bloomLevel,
        string? topic,
        int? materialId,
        int decision, // 1 = Approve, 2 = Reject
        string? notes,
        CancellationToken cancellationToken)
    {
        var current = await _documentService.GetQuestionAsync(questionId, cancellationToken);
        if (!current.Success || current.Data is null) return NotFound();
        DocumentId = current.Data.DocumentId;
        Question = current.Data with { QuestionText = questionText ?? string.Empty, AnswerKey = answerKey, Explanation = explanation, Topic = topic };
        MaterialId = materialId;
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(questionText))
        {
            Error = "Vui lòng nhập nội dung câu hỏi hợp lệ.";
            await LoadMaterialOptionsAsync(cancellationToken);
            return Page();
        }
        var reviewerId = HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        if (decision is not (1 or 2)) return BadRequest();
        var status = decision == 1 ? GeneratedQuestionStatus.Approved : GeneratedQuestionStatus.Rejected;

        var request = new ReviewQuestionRequest(status, notes, questionText, answerKey ?? string.Empty, explanation ?? string.Empty, (DocumentBloomLevel)bloomLevel, topic ?? string.Empty, materialId);
        var response = await _documentService.ReviewQuestionAsync(questionId, reviewerId, request, cancellationToken);

        if (response.Success)
        {
            TempData["Success"] = decision == 1 ? "Đã duyệt câu hỏi." : "Đã từ chối câu hỏi.";
        }
        else
        {
            Error = response.Error;
            await LoadMaterialOptionsAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("Index", new { documentId = DocumentId });
    }

    private async Task LoadMaterialOptionsAsync(CancellationToken ct)
    {
        var document = (await _documentService.GetAsync(DocumentId, ct)).Data;
        HasSourceMaterial = document?.MaterialId is not null;
        MaterialId ??= document?.MaterialId;
        var courseId = Question?.CourseId ?? document?.CourseId;
        if (courseId is null) return;
        var lecturerId = HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
            ? HttpContext.Session.GetInt32(SessionKeys.UserId)
            : null;
        var result = await _materials.ListAsync(lecturerId, courseId, ct);
        MaterialOptions = (result.Data ?? [])
            .Select(x => new SelectListItem(x.FileName, x.MaterialId.ToString(), x.MaterialId == MaterialId))
            .ToList();
    }

    public string GetQuestionStatusBadgeClass(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "bg-warning text-dark",
        GeneratedQuestionStatus.Approved => "bg-success",
        GeneratedQuestionStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    public string GetQuestionStatusText(GeneratedQuestionStatus status) => status switch
    {
        GeneratedQuestionStatus.PendingReview => "Chờ duyệt",
        GeneratedQuestionStatus.Approved => "Đã duyệt",
        GeneratedQuestionStatus.Rejected => "Đã từ chối",
        _ => "Không xác định"
    };
}