using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// One multiple-choice question in the bank. A question always belongs to a course,
/// optionally points at the course material it was written from, and carries exactly
/// one <see cref="QuestionOption"/> marked correct.
/// </summary>
public class Question
{
    public int QuestionId { get; set; }

    /// <summary>The course the question is banked under; only questions of this course reach an exam.</summary>
    public int CourseId { get; set; }

    /// <summary>The course material this question was derived from; used as its topic.</summary>
    public int? SourceMaterialId { get; set; }

    /// <summary>The account that wrote the question.</summary>
    public int CreatedBy { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    /// <summary>What a satisfactory answer must contain; shown to the examiner, never to the student.</summary>
    public string? ExpectedAnswer { get; set; }

    public BloomLevel BloomLevel { get; set; }

    public QuestionDifficulty Difficulty { get; set; }

    /// <summary>
    /// Follow-up questions are owned by the results module; the bank only creates
    /// <see cref="QuestionType.Main"/> questions.
    /// </summary>
    public QuestionType QuestionType { get; set; } = QuestionType.Main;

    public QuestionStatus Status { get; set; } = QuestionStatus.Approved;

    /// <summary>Whether a person or the AI wrote it; AI questions are held for review.</summary>
    public QuestionSource Source { get; set; } = QuestionSource.Manual;

    /// <summary>The reviewer who approved or rejected it; null while it is still pending.</summary>
    public int? ReviewedBy { get; set; }

    /// <summary>When the review decision was made; null while it is still pending.</summary>
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public User CreatedByUser { get; set; } = null!;

    public CourseMaterial? SourceMaterial { get; set; }

    public Rubric? Rubric { get; set; }

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}
