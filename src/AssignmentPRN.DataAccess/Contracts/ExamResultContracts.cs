using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

/// <summary>The session a results screen is about, with who owns it.</summary>
public sealed class ExamResultSession
{
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

    public ExamSessionStatus Status { get; init; }
}

/// <summary>One candidate of a session with every graded slot of their paper.</summary>
public sealed class CandidateResultRow
{
    public int CandidateId { get; init; }

    public int ExamId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public DateTime? ScheduledTime { get; init; }

    public CandidateStatus Status { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public IReadOnlyList<GradedSlot> Slots { get; init; } = Array.Empty<GradedSlot>();
}

/// <summary>Whether one slot of a paper is a follow-up and whether it was answered right.</summary>
public sealed class GradedSlot
{
    public bool IsFollowUp { get; init; }

    public bool IsAnswered { get; init; }

    public bool IsCorrect { get; init; }
}
