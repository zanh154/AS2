namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// What a candidate answered for one exam slot. The table was designed for the spoken
/// part of a viva (<see cref="Transcript"/>, <see cref="AudioPath"/>); the question bank
/// only fills <see cref="SelectedOptionId"/>, which records the choice they ticked.
/// </summary>
public class Answer
{
    public int AnswerId { get; set; }

    public int ExamQuestionId { get; set; }

    public int CandidateId { get; set; }

    /// <summary>
    /// Choice the student ticked. Null for a question they left blank, and for a spoken
    /// answer that has no choices at all.
    /// </summary>
    public int? SelectedOptionId { get; set; }

    public string? Transcript { get; set; }

    public string? AudioPath { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }

    public ExamQuestion ExamQuestion { get; set; } = null!;

    public QuestionOption? SelectedOption { get; set; }
}
