using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.Policies;

/// <summary>Pure decisions about session progress and overdue candidates; no persistence.</summary>
public static class ExamLifecycleRules
{
    public static ExamSessionStatus SessionAfterProgress(ExamSessionStatus current, IEnumerable<CandidateStatus> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (!ExamSessionRules.CanSit(current)) return current;
        var states = candidates.ToList();
        if (states.Count == 0) return current;
        if (states.All(x => x is CandidateStatus.Completed or CandidateStatus.Absent or CandidateStatus.Cancelled))
            return ExamSessionStatus.Completed;
        return states.Any(x => x is CandidateStatus.InProgress or CandidateStatus.Completed)
            ? ExamSessionStatus.InProgress : current;
    }

    /// <summary>Null means no change; exact end/grace boundaries still accept the current state.</summary>
    public static CandidateStatus? CloseOverdue(CandidateStatus current, DateTime? scheduled, int minutes, DateTime now)
    {
        if (scheduled is not DateTime start) return null;
        var end = start.AddMinutes(minutes);
        if (current == CandidateStatus.Waiting && now > end) return CandidateStatus.Absent;
        if (current == CandidateStatus.InProgress && now > end + ExamSessionRules.SubmitGrace) return CandidateStatus.Completed;
        return null;
    }

    public static CandidateStatus? CloseOverdue(ExamSessionStatus session, CandidateStatus current,
        DateTime? scheduled, int minutes, DateTime now) =>
        ExamSessionRules.CanSit(session) ? CloseOverdue(current, scheduled, minutes, now) : null;
}
