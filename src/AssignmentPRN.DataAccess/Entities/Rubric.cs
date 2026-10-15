using AssignmentPRN.DataAccess.Entities;

/// <summary>
/// A rubric groups criteria (max_score, display_order) under one question.
/// Each question has at most one rubric (unique key question_id).
/// </summary>
public class Rubric
{
    public int RubricId { get; set; }
    public int QuestionId { get; set; }
    public decimal TotalScore { get; set; } = 0m;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public virtual Question Question { get; set; } = null!;
    public virtual List<RubricCriteria> Criteria { get; set; } = new();
}