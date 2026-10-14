using Xunit;
using Biz = AssignmentPRN.Business.Interfaces;
using BRules = AssignmentPRN.Business.Policies;
using Data = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Tests;

/// <summary>
/// Business owns the policy; the persistence adapter exposes it over data access
/// enums. These tests verify the mappings preserve decisions.
/// </summary>
public class SharedExamSessionRulesTests
{
    [Fact]
    public void Enum_names_and_values_match_across_facades()
    {
        Assert.Equal(Enum.GetNames<Data.ExamSessionStatus>(), Enum.GetNames<Biz.ExamSessionStatus>());
        Assert.Equal(Enum.GetNames<Data.CandidateStatus>(), Enum.GetNames<Biz.CandidateStatus>());
        foreach (var name in Enum.GetNames<Data.ExamSessionStatus>())
            Assert.Equal((int)Enum.Parse<Data.ExamSessionStatus>(name), (int)Enum.Parse<Biz.ExamSessionStatus>(name));
        foreach (var name in Enum.GetNames<Data.CandidateStatus>())
            Assert.Equal((int)Enum.Parse<Data.CandidateStatus>(name), (int)Enum.Parse<Biz.CandidateStatus>(name));
    }

    [Fact]
    public void Both_layers_use_the_same_transition_and_cancellation_policies()
    {
        foreach (var from in Enum.GetValues<Data.ExamSessionStatus>())
        {
            Assert.Equal(new BRules.ExamStatePolicy().CanEdit(from), BRules.ExamSessionRules.CanEdit((Biz.ExamSessionStatus)from));
            Assert.Equal(new BRules.ExamStatePolicy().CanSit(from), BRules.ExamSessionRules.CanSit((Biz.ExamSessionStatus)from));
            foreach (var to in Enum.GetValues<Data.ExamSessionStatus>())
                Assert.Equal(new BRules.ExamStatePolicy().CanTransition(from, to), BRules.ExamSessionRules.CanTransition((Biz.ExamSessionStatus)from, (Biz.ExamSessionStatus)to));
        }
        foreach (var status in Enum.GetValues<Data.CandidateStatus>())
        {
            Assert.Equal(new BRules.ExamStatePolicy().BlocksCancellation(status), BRules.ExamSessionRules.BlocksCancellation((Biz.CandidateStatus)status));
            Assert.Equal((int)new BRules.ExamStatePolicy().StatusAfterCancellation(status), (int)BRules.ExamSessionRules.StatusAfterCancellation((Biz.CandidateStatus)status));
        }
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(0, true, true)]
    [InlineData(600, true, true)]
    [InlineData(601, false, true)]
    [InlineData(720, false, true)]
    [InlineData(721, false, false)]
    public void Slot_and_submission_boundaries_are_preserved(int seconds, bool canSit, bool canSubmit)
    {
        var start = new DateTime(2030, 1, 1, 8, 0, 0);
        var now = start.AddSeconds(seconds);
        var end = start.AddMinutes(10);
        Assert.Equal(canSit, new BRules.ExamStatePolicy().IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, BRules.ExamSessionRules.IsSlotOpen(now, start, 10));
        Assert.Equal(canSit, BRules.ExamSessionRules.IsSlotOpen(now, start, end));
        Assert.Equal(canSubmit, BRules.ExamSessionRules.CanSubmit(now, start, end));
    }
}
