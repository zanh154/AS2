using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Rubrics;

/// <summary>
/// Creates or edits the rubric of a question. A rubric belongs to exactly one question,
/// so the question id is the key this page works with; <see cref="RubricId"/> is 0 while
/// creating and only gets a value once the question already has a saved rubric.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class EditModel(
    IRubricService rubricService,
    IQuestionService questionService) : PageModel
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    [BindProperty]
    public int RubricId { get; set; }

    [BindProperty]
    public int QuestionId { get; set; }

    public string QuestionText { get; private set; } = string.Empty;

    public decimal TotalScore { get; private set; }

    [BindProperty]
    public List<RubricCriteriaRow> Criteria { get; set; } = [];

    public IReadOnlyList<SelectListItem> QuestionOptions { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? CourseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Term { get; set; }

    public string? LoadError { get; private set; }

    public class RubricCriteriaRow
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MaxScore { get; set; }
        public int DisplayOrder { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? questionId, CancellationToken ct)
    {
        QuestionId = questionId ?? 0;

        if (QuestionId > 0)
        {
            var questionResult = await questionService.GetAsync(QuestionId, LecturerId, ct);
            if (!questionResult.Success || questionResult.Data is null)
            {
                TempData["Error"] = questionResult.Error ?? "Không tìm thấy câu hỏi.";
                return RedirectToPage("Index", new { courseId = CourseId, term = Term });
            }

            QuestionText = questionResult.Data.QuestionText;

            // A question has at most one rubric. Load it when it exists so the page edits it
            // instead of creating a duplicate; a question without one opens an empty form.
            var existing = await rubricService.GetAsync(QuestionId, LecturerId, ct);
            if (existing.Success && existing.Data is not null)
            {
                RubricId = existing.Data.RubricId;
                TotalScore = existing.Data.TotalScore;
                Criteria = existing.Data.Criteria
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new RubricCriteriaRow
                    {
                        CriteriaId = c.CriteriaId,
                        CriteriaName = c.CriteriaName,
                        Description = c.Description,
                        MaxScore = c.MaxScore,
                        DisplayOrder = c.DisplayOrder
                    })
                    .ToList();
            }
        }

        await LoadQuestionOptionsAsync(ct);

        if (Criteria.Count == 0)
        {
            Criteria.Add(new RubricCriteriaRow { DisplayOrder = 1 });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Validate();

        if (QuestionId <= 0)
        {
            ModelState.AddModelError(nameof(QuestionId), "Vui lòng chọn câu hỏi.");
        }

        if (ModelState.IsValid)
        {
            var saveRequest = new RubricSaveRequest
            {
                RubricId = RubricId,
                QuestionId = QuestionId,
                Criteria = Criteria
                    .Select(c => new RubricCriteriaInput
                    {
                        CriteriaId = c.CriteriaId,
                        CriteriaName = c.CriteriaName,
                        Description = c.Description,
                        MaxScore = c.MaxScore,
                        DisplayOrder = c.DisplayOrder
                    })
                    .ToList()
            };

            var result = await rubricService.SaveAsync(saveRequest, LecturerId, ct);
            if (result.Success)
            {
                TempData["Success"] = RubricId == 0
                    ? "Đã tạo bộ tiêu chí."
                    : "Đã cập nhật bộ tiêu chí.";
                return RedirectToPage("Index", new { courseId = CourseId, term = Term });
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể lưu bộ tiêu chí.");
        }

        await LoadQuestionOptionsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAddCriterionAsync(CancellationToken ct)
    {
        var newOrder = Criteria.Count > 0 ? Criteria.Max(c => c.DisplayOrder) + 1 : 1;
        Criteria.Add(new RubricCriteriaRow { DisplayOrder = newOrder });
        await LoadQuestionOptionsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveCriterionAsync(int index, CancellationToken ct)
    {
        if (index >= 0 && index < Criteria.Count)
        {
            Criteria.RemoveAt(index);
        }

        // A rubric always needs at least one criterion, and the display order stays dense so
        // the form never shows a gap after a row is removed.
        if (Criteria.Count == 0)
        {
            Criteria.Add(new RubricCriteriaRow { DisplayOrder = 1 });
        }
        else
        {
            for (var i = 0; i < Criteria.Count; i++)
            {
                Criteria[i].DisplayOrder = i + 1;
            }
        }

        await LoadQuestionOptionsAsync(ct);
        return Page();
    }

    private void Validate()
    {
        if (Criteria.Count == 0)
        {
            ModelState.AddModelError(nameof(Criteria), "Bộ tiêu chí phải có ít nhất một tiêu chí.");
            return;
        }

        for (var i = 0; i < Criteria.Count; i++)
        {
            var criterion = Criteria[i];
            if (string.IsNullOrWhiteSpace(criterion.CriteriaName))
            {
                ModelState.AddModelError($"Criteria[{i}].CriteriaName", "Tên tiêu chí không được để trống.");
            }

            if (criterion.MaxScore <= 0)
            {
                ModelState.AddModelError($"Criteria[{i}].MaxScore", "Điểm tối đa phải lớn hơn 0.");
            }
        }

        var duplicateOrders = Criteria
            .GroupBy(c => c.DisplayOrder)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateOrders.Count > 0)
        {
            ModelState.AddModelError(string.Empty, $"Thứ tự hiển thị bị trùng: {string.Join(", ", duplicateOrders)}.");
        }
    }

    private async Task LoadQuestionOptionsAsync(CancellationToken ct)
    {
        if (QuestionId > 0)
        {
            if (string.IsNullOrEmpty(QuestionText))
            {
                var questionResult = await questionService.GetAsync(QuestionId, LecturerId, ct);
                if (questionResult.Success && questionResult.Data is not null)
                {
                    QuestionText = questionResult.Data.QuestionText;
                }
            }

            QuestionOptions = string.IsNullOrEmpty(QuestionText)
                ? []
                : [new SelectListItem(QuestionText, QuestionId.ToString())];
            return;
        }

        var listResult = await questionService.ListAsync(LecturerId, includeArchived: false, cancellationToken: ct);
        if (!listResult.Success || listResult.Data is null)
        {
            LoadError = listResult.Error;
            QuestionOptions = [];
            return;
        }

        // A question owns at most one rubric, so a question that already has one is edited
        // from the list page instead of being offered here again.
        var rubricResult = await rubricService.ListAsync(LecturerId, cancellationToken: ct);
        var questionsWithRubric = rubricResult.Success && rubricResult.Data is not null
            ? rubricResult.Data.Select(rubric => rubric.QuestionId).ToHashSet()
            : new HashSet<int>();

        QuestionOptions = listResult.Data
            .Where(question => question.Status == QuestionStatus.Approved
                && !questionsWithRubric.Contains(question.QuestionId))
            .OrderBy(question => question.CourseCode)
            .ThenBy(question => question.QuestionText)
            .Select(question => new SelectListItem(
                $"{question.CourseCode} - {Truncate(question.QuestionText, 80)}",
                question.QuestionId.ToString()))
            .ToList();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }
}
