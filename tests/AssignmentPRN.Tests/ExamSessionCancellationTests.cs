using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class ExamSessionCancellationTests
{
    [Fact]
    public void Only_a_student_still_answering_blocks_cancelling_the_session()
    {
        Assert.True(ExamSessionRules.BlocksCancellation(CandidateStatus.InProgress));

        Assert.False(ExamSessionRules.BlocksCancellation(CandidateStatus.Waiting));
        Assert.False(ExamSessionRules.BlocksCancellation(CandidateStatus.Completed));
        Assert.False(ExamSessionRules.BlocksCancellation(CandidateStatus.Absent));
        Assert.False(ExamSessionRules.BlocksCancellation(CandidateStatus.Cancelled));
    }

    [Fact]
    public void A_slot_not_sat_yet_is_cancelled_with_its_session()
    {
        Assert.Equal(
            CandidateStatus.Cancelled,
            ExamSessionRules.StatusAfterCancellation(CandidateStatus.Waiting));
    }

    [Theory]
    [InlineData(CandidateStatus.Completed)]
    [InlineData(CandidateStatus.Absent)]
    [InlineData(CandidateStatus.Cancelled)]
    public void A_slot_with_a_record_keeps_it_when_the_session_is_cancelled(CandidateStatus status)
    {
        Assert.Equal(status, ExamSessionRules.StatusAfterCancellation(status));
    }

    [Theory]
    [InlineData(ExamSessionStatus.Draft)]
    [InlineData(ExamSessionStatus.Scheduled)]
    [InlineData(ExamSessionStatus.InProgress)]
    public void An_open_session_can_still_be_cancelled(ExamSessionStatus from)
    {
        Assert.True(ExamSessionRules.CanTransition(from, ExamSessionStatus.Cancelled));
    }

    [Theory]
    [InlineData(ExamSessionStatus.Completed)]
    [InlineData(ExamSessionStatus.Cancelled)]
    public void A_closed_session_cannot_be_cancelled(ExamSessionStatus from)
    {
        Assert.False(ExamSessionRules.CanTransition(from, ExamSessionStatus.Cancelled));
    }
}
