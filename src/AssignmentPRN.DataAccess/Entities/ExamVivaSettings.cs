namespace AssignmentPRN.DataAccess.Entities;
public sealed class ExamVivaSettings
{
    public int ExamId { get; set; }
    public int SecondsPerQuestion { get; set; } = 120;
    public int MaxFollowUpsPerQuestion { get; set; } = 1;
}
