namespace AssignmentPRN.DataAccess.Contracts;

public class ExamSessionUpdateInput
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public int TimePerStudent { get; set; }
    public int MainQuestionCount { get; set; }
    public int MaxFollowUpCount { get; set; }
}
