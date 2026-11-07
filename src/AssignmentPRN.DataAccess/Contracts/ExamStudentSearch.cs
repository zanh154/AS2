using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

public sealed class ExamStudentSearch
{
    public string? Query { get; set; }
    public int? ExamId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public CandidateStatus? Status { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record ExamStudentRow(int CandidateId, int ExamId, string StudentName,
    string Email, string ExamName, string CourseName, DateTime? ScheduledTime, CandidateStatus Status);
public sealed record ExamStudentSessionOption(int ExamId, string ExamName);
public sealed class ExamStudentSearchResult
{
    public IReadOnlyList<ExamStudentRow> Items { get; init; } = [];
    public IReadOnlyList<ExamStudentSessionOption> Sessions { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize => 20;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
