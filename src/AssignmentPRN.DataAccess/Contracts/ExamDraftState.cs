using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

/// <summary>Database snapshot read within the transaction that will save the draft.</summary>
public sealed class ExamDraftState
{
    public int StudentId { get; init; }
    public CandidateStatus CandidateStatus { get; init; }
    public ExamSessionStatus SessionStatus { get; init; }
    public DateTime? ScheduledTime { get; init; }
    public int TimePerStudent { get; init; }
    public IReadOnlyList<ExamDraftQuestion> Questions { get; init; } = [];
}

public sealed class ExamDraftQuestion
{
    public int ExamQuestionId { get; init; }
    public bool IsFollowUp { get; init; }
    public bool IsSubmitted { get; init; }
    public IReadOnlySet<int> OptionIds { get; init; } = new HashSet<int>();
}
