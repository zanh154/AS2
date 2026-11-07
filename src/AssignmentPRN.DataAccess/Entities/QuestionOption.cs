namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// One selectable answer of a <see cref="Question"/>. A question needs at least
/// two options and exactly one of them marked correct.
/// </summary>
public class QuestionOption
{
    public int OptionId { get; set; }

    public int QuestionId { get; set; }

    public string OptionText { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    /// <summary>1-based position shown to the student (A, B, C, ...).</summary>
    public int DisplayOrder { get; set; }

    public Question Question { get; set; } = null!;
}
