namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// A single criteria item under a rubric: name, description, max_score, display_order.
/// </summary>
public class RubricCriteria
{
    public int CriteriaId { get; set; }
    public int RubricId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; } = 0m;
    public int DisplayOrder { get; set; } = 1;

    // Navigation
    public virtual Rubric Rubric { get; set; } = null!;
}