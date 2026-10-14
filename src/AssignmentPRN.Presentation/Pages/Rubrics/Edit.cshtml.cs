using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssignmentPRN.Presentation.Pages.Rubrics;

/// <summary>
/// Creates or edits a rubric with its criteria.
/// </summary>
[SessionAuthorize(RoleNames.Admin, RoleNames.Lecturer)]
public class EditModel(
    IRubricService rubricService,
    IQuestionService questionService) : PageModel
{
    private int? LecturerId => HttpContext.Session.GetString(SessionKeys.Role) == RoleNames.Lecturer
        ? HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0
        : null;

    public int RubricId { get; private set; }

    public int QuestionId { get; private set; }

    public string QuestionText { get; private set; } = string.Empty;

    public decimal TotalScore { get; private set; }

    public List<RubricCriteriaRow> Criteria { get; set; } = [];

    public IReadOnlyList<SelectListItem> QuestionOptions { get; private set; } = [];

    [BindProperty]
    public RubricSaveInput Input { get; set; } = new();

    public string? LoadError { get; private set; }

    public class RubricCriteriaRow
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MaxScore { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class RubricSaveInput
    {
        public int RubricId { get; set; }
        public int QuestionId { get; set; }
        public List<RubricCriteriaInput> Criteria { get; set; } = [];
    }

    public class RubricCriteriaInput
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MaxScore { get; set; }
        public int DisplayOrder { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id, int? questionId, int? courseId, string? term, CancellationToken ct)
    {
        RubricId = id ?? 0;
        QuestionId = questionId ?? 0;

        if (RubricId > 0)
        {
            var result = await rubricService.GetAsync(QuestionId, LecturerId, ct);
            if (!result.Success || result.Data is null)
            {
                TempData["Error"] = result.Error;
                return RedirectToPage("Index", new { courseId, term });
            }

            var detail = result.Data;
            RubricId = detail.RubricId;
            QuestionId = detail.QuestionId;
            QuestionText = detail.QuestionText;
            TotalScore = detail.TotalScore;
            Criteria = detail.Criteria
                .Select(c => new RubricCriteriaRow
                {
                    CriteriaId = c.CriteriaId,
                    CriteriaName = c.CriteriaName,
                    Description = c.Description,
                    MaxScore = c.MaxScore,
                    DisplayOrder = c.DisplayOrder
                })
                .OrderBy(c => c.DisplayOrder)
                .ToList();
        }
        else if (QuestionId > 0)
        {
            var qResult = await questionService.GetAsync(QuestionId, LecturerId, ct);
            if (!qResult.Success || qResult.Data is null)
            {
                TempData["Error"] = qResult.Error;
                return RedirectToPage("Index", new { courseId, term });
            }
            QuestionText = qResult.Data.QuestionText;
        }

        await LoadQuestionsAsync(ct);

        // If no criteria yet, add one empty row
        if (Criteria.Count == 0)
        {
            Criteria.Add(new RubricCriteriaRow { DisplayOrder = 1 });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? courseId, string? term, CancellationToken ct)
    {
        if (Criteria.Count == 0)
        {
            ModelState.AddModelError(nameof(Criteria), "Bộ tiêu chí phải có ít nhất một tiêu chí.");
        }

        for (int i = 0; i < Criteria.Count; i++)
        {
            var c = Criteria[i];
            if (string.IsNullOrWhiteSpace(c.CriteriaName))
            {
                ModelState.AddModelError($"Criteria[{i}].CriteriaName", "Tên tiêu chí không được để trống.");
            }
            if (c.MaxScore <= 0)
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

        if (ModelState.IsValid)
        {
            var saveRequest = new RubricSaveRequest
            {
                RubricId = RubricId,
                QuestionId = QuestionId,
                Criteria = Criteria
                    .Select(c => new AssignmentPRN.Business.Interfaces.RubricCriteriaInput
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
                return RedirectToPage("Index", new { courseId, term });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await LoadQuestionsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAddCriterionAsync(int? courseId, string? term, CancellationToken ct)
    {
        var newOrder = Criteria.Count > 0 ? Criteria.Max(c => c.DisplayOrder) + 1 : 1;
        Criteria.Add(new RubricCriteriaRow { DisplayOrder = newOrder });
        await LoadQuestionsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveCriterionAsync(int index, int? courseId, string? term, CancellationToken ct)
    {
        if (index >= 0 && index < Criteria.Count)
        {
            Criteria.RemoveAt(index);
        }
        await LoadQuestionsAsync(ct);
        return Page();
    }

    private async Task LoadQuestionsAsync(CancellationToken ct)
    {
        if (QuestionId > 0)
        {
            var qResult = await questionService.GetAsync(QuestionId, LecturerId, ct);
            if (qResult.Success && qResult.Data is not null)
            {
                QuestionText = qResult.Data.QuestionText;
                QuestionOptions = [new SelectListItem(qResult.Data.QuestionText, qResult.Data.QuestionId.ToString())];
            }
        }
        else
        {
            var listResult = await questionService.ListAsync(LecturerId, includeArchived: false, cancellationToken: ct);
            QuestionOptions = (listResult.Data ?? [])
                .Where(q => q.Status == QuestionStatus.Approved)
                .OrderBy(q => q.CourseCode)
                .ThenBy(q => q.QuestionText)
                .Select(q => new SelectListItem($"{q.CourseCode} - {q.QuestionText[..Math.Min(80, q.QuestionText.Length)]}", q.QuestionId.ToString()))
                .ToList();
        }
    }
}