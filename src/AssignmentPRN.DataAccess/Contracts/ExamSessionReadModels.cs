using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

public sealed class ExamSessionListItem
{
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public ExamSessionStatus Status { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int CandidateCount { get; init; }
}

public sealed class ExamSessionDetail
{
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

    public ExamSessionStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public PersonSummary Lecturer { get; init; } = new();

    public CourseSummary Course { get; init; } = new();

    public IReadOnlyList<ExamCandidateDetail> Candidates { get; init; } = Array.Empty<ExamCandidateDetail>();
}

public sealed class PersonSummary
{
    public int UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}

public sealed class CourseSummary
{
    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class ExamCandidateDetail
{
    public int CandidateId { get; init; }

    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public DateTime? ScheduledTime { get; init; }

    public CandidateStatus Status { get; init; }
}

public sealed class StudentSchedule
{
    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public IReadOnlyList<StudentScheduleItem> Items { get; init; } = Array.Empty<StudentScheduleItem>();
}

public sealed class StudentScheduleItem
{
    public int ExamId { get; init; }

    public int CandidateId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

    public DateTime ScheduledTime { get; init; }

    public int TimePerStudent { get; init; }

    public ExamSessionStatus SessionStatus { get; init; }

    public CandidateStatus CandidateStatus { get; init; }
}
