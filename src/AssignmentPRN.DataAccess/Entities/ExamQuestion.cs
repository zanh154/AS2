namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// One question handed to one candidate inside an exam. The question bank writes
/// these rows when an exam is created; the results module reads and updates them
/// while the student answers.
/// </summary>
public class ExamQuestion
{
    public int ExamQuestionId { get; set; }

    public int ExamId { get; set; }

    public int CandidateId { get; set; }

    public int QuestionId { get; set; }

    /// <summary>
    /// The main question this one follows up on. Null for a main question; set for a
    /// follow-up dealt after the student handed in the main round.
    /// </summary>
    public int? ParentExamQuestionId { get; set; }

    /// <summary>1-based position of the question in the candidate's paper.</summary>
    public int OrderNo { get; set; }

    public DateTime? AskedAt { get; set; }

    /// <summary>Set once the student has answered this question.</summary>
    public bool IsCompleted { get; set; }

    public ExamSession Session { get; set; } = null!;

    public ExamCandidate Candidate { get; set; } = null!;

    public Question Question { get; set; } = null!;
}
