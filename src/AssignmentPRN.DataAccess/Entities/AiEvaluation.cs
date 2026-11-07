namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// What the AI suggested for one spoken answer after reading its transcript against the
/// rubric of the question. A suggestion only: the lecturer decides the mark that counts,
/// which is kept in <see cref="FinalResult"/>.
/// </summary>
public class AiEvaluation
{
    public int EvaluationId { get; set; }

    /// <summary>One evaluation per answer; grading again overwrites this row.</summary>
    public int AnswerId { get; set; }

    /// <summary>Suggested mark out of ten for this answer.</summary>
    public decimal AiScore { get; set; }

    /// <summary>How sure the model said it was, 0 to 1. Null when it did not say.</summary>
    public decimal? Confidence { get; set; }

    public string? Feedback { get; set; }

    public string? Strengths { get; set; }

    public string? Weaknesses { get; set; }

    public string? MissingPoints { get; set; }

    public DateTime EvaluatedAt { get; set; }

    public Answer Answer { get; set; } = null!;

    /// <summary>One row per rubric criterion the AI marked.</summary>
    public List<AiEvaluationCriteria> Criteria { get; set; } = [];
}

/// <summary>
/// The mark and note the AI gave one rubric criterion. Kept apart from
/// <see cref="AiEvaluation"/> because the examiner needs to see where the marks came
/// from, not just the total.
/// </summary>
public class AiEvaluationCriteria
{
    public int EvaluationCriteriaId { get; set; }

    public int EvaluationId { get; set; }

    public int CriteriaId { get; set; }

    /// <summary>Never above the criterion's own max_score; clamped before it is stored.</summary>
    public decimal Score { get; set; }

    public string? Comment { get; set; }

    public AiEvaluation Evaluation { get; set; } = null!;

    public RubricCriteria Criteria { get; set; } = null!;
}
