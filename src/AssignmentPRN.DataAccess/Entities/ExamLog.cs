namespace AssignmentPRN.DataAccess.Entities;

/// <summary>Audit events in the existing AIVES exam_logs table.</summary>
public sealed class ExamLog
{
    public long LogId { get; set; }
    public int ExamId { get; set; }
    public int? CandidateId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}
