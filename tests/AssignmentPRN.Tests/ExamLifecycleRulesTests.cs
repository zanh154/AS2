using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Services;
using Xunit;

namespace AssignmentPRN.Tests;

public class ExamLifecycleRulesTests
{
    [Theory]
    [InlineData(ExamSessionStatus.Scheduled, CandidateStatus.Waiting, ExamSessionStatus.Scheduled)]
    [InlineData(ExamSessionStatus.Scheduled, CandidateStatus.InProgress, ExamSessionStatus.InProgress)]
    [InlineData(ExamSessionStatus.Scheduled, CandidateStatus.Completed, ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Scheduled, CandidateStatus.Absent, ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.InProgress, CandidateStatus.Waiting, ExamSessionStatus.InProgress)]
    [InlineData(ExamSessionStatus.InProgress, CandidateStatus.Completed, ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Draft, CandidateStatus.Completed, ExamSessionStatus.Draft)]
    [InlineData(ExamSessionStatus.Completed, CandidateStatus.InProgress, ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Cancelled, CandidateStatus.Completed, ExamSessionStatus.Cancelled)]
    public void Session_progress_preserves_lifecycle(ExamSessionStatus current, CandidateStatus candidate, ExamSessionStatus expected) =>
        Assert.Equal(expected, ExamLifecycleRules.SessionAfterProgress(current, [candidate]));

    [Fact]
    public void Mixed_roster_finishes_only_after_every_slot_is_terminal()
    {
        Assert.Equal(ExamSessionStatus.InProgress, ExamLifecycleRules.SessionAfterProgress(ExamSessionStatus.Scheduled,
            [CandidateStatus.Completed, CandidateStatus.Waiting]));
        Assert.Equal(ExamSessionStatus.Completed, ExamLifecycleRules.SessionAfterProgress(ExamSessionStatus.InProgress,
            [CandidateStatus.Completed, CandidateStatus.Absent, CandidateStatus.Cancelled]));
        Assert.Equal(ExamSessionStatus.Scheduled, ExamLifecycleRules.SessionAfterProgress(ExamSessionStatus.Scheduled, []));
    }

    [Theory]
    [InlineData(ExamSessionStatus.Draft)]
    [InlineData(ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Cancelled)]
    public void Closing_overdue_does_not_change_non_open_sessions(ExamSessionStatus status)
    {
        var start = new DateTime(2030, 1, 1);
        Assert.Null(ExamLifecycleRules.CloseOverdue(status, CandidateStatus.Waiting, start, 10, start.AddDays(1)));
        Assert.Null(ExamLifecycleRules.CloseOverdue(status, CandidateStatus.InProgress, start, 10, start.AddDays(1)));
    }

    [Fact]
    public void Overdue_boundaries_and_settled_candidates_are_preserved()
    {
        var start = new DateTime(2030, 1, 1);
        Assert.Null(ExamLifecycleRules.CloseOverdue(CandidateStatus.Waiting, start, 10, start.AddMinutes(10)));
        Assert.Equal(CandidateStatus.Absent, ExamLifecycleRules.CloseOverdue(CandidateStatus.Waiting, start, 10, start.AddMinutes(10).AddTicks(1)));
        Assert.Null(ExamLifecycleRules.CloseOverdue(CandidateStatus.InProgress, start, 10, start.AddMinutes(12)));
        Assert.Equal(CandidateStatus.Completed, ExamLifecycleRules.CloseOverdue(CandidateStatus.InProgress, start, 10, start.AddMinutes(12).AddTicks(1)));
        foreach (var status in new[] { CandidateStatus.Completed, CandidateStatus.Absent, CandidateStatus.Cancelled })
            Assert.Null(ExamLifecycleRules.CloseOverdue(status, start, 10, start.AddDays(1)));
        Assert.Null(ExamLifecycleRules.CloseOverdue(CandidateStatus.Waiting, null, 10, start));
    }
}
