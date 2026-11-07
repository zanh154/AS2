using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>
/// The "phát đề" screen of an exam: how many questions each student gets and which
/// slice of the bank they are drawn from. The same model carries the papers back so
/// the lecturer sees the result of the draw without a second round-trip.
/// </summary>
public class ExamQuestionAssignViewModel
{
    [Range(1, int.MaxValue)]
    public int ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public int CourseId { get; set; }

    [Display(Name = "Số câu hỏi chính mỗi sinh viên")]
    [Range(1, 50, ErrorMessage = "Số câu hỏi chính phải từ 1 đến 50.")]
    public int CountPerCandidate { get; set; } = 3;

    [Display(Name = "Chủ đề (tài liệu)")]
    public List<int> MaterialIds { get; set; } = [];

    [Display(Name = "Độ khó")]
    public List<QuestionDifficulty> Difficulties { get; set; } = [];

    public IReadOnlyList<SelectListItem> MaterialOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> DifficultyOptions { get; set; } = Array.Empty<SelectListItem>();

    /// <summary>Candidates with the paper they already hold; empty papers mean "chưa phát".</summary>
    public IReadOnlyList<ExamPaperItem> Papers { get; set; } = Array.Empty<ExamPaperItem>();

    /// <summary>Number of distinct, still-free questions matching the current filter.</summary>
    public int AvailableCount { get; set; }

    public int AvailableManualCount { get; set; }

    public int AvailableAiCount { get; set; }

    public int CandidateCount => Papers.Count;

    public int AssignedCandidateCount => Papers.Count(paper => paper.Questions.Count > 0);

    /// <summary>Questions the draw needs for the students that do not have a paper yet.</summary>
    public int RequiredCount => (CandidateCount - AssignedCandidateCount) * CountPerCandidate;

    public bool IsEnough => AvailableCount >= RequiredCount;

    public bool IsFullyAssigned => CandidateCount > 0 && AssignedCandidateCount == CandidateCount;

    /// <summary>False once a student has started answering: the draw is then frozen.</summary>
    public bool CanRedeal { get; set; }

    /// <summary>True while the exam is still editable; a closed exam is read-only here.</summary>
    public bool CanAssign { get; set; }

    public string? LoadError { get; set; }
}

/// <summary>What the student's exam room renders. The response carries no answer key.</summary>
public class ExamRoomViewModel
{
    public ExamRoomResponse Room { get; init; } = new();

    /// <summary>Position of a main slot in the paper, so a follow-up can say which question it digs into.</summary>
    public int? OrderOf(int? examQuestionId) => Room.Questions
        .FirstOrDefault(question => question.ExamQuestionId == examQuestionId)?.OrderNo;
}

/// <summary>One question of the exam room, answerable or shown as handed in.</summary>
public class ExamRoomQuestionViewModel
{
    public ExamRoomQuestion Question { get; init; } = new();

    /// <summary>True renders the radio group; false shows the pick read-only.</summary>
    public bool Editable { get; init; }

    /// <summary>For a follow-up, the number of the main question it digs into.</summary>
    public int? ParentOrderNo { get; init; }
}

public class ExamResultViewModel
{
    public ExamResultResponse Result { get; init; } = new();

    /// <summary>Position of a main slot in the paper, so a follow-up can say which question it digs into.</summary>
    public int? OrderOf(int? examQuestionId) => Result.Questions
        .FirstOrDefault(question => question.ExamQuestionId == examQuestionId)?.OrderNo;
}

/// <summary>One graded question, as the student or the examiner reviews it.</summary>
public class ExamResultQuestionViewModel
{
    public ExamResultQuestion Question { get; init; } = new();

    /// <summary>For a follow-up, the number of the main question it digs into.</summary>
    public int? ParentOrderNo { get; init; }

    /// <summary>Note beside the ticked choice; the examiner reads it about someone else.</summary>
    public string PickedLabel { get; init; } = "Bạn chọn";
}
