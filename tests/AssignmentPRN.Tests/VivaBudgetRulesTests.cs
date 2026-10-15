using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using Xunit;
namespace AssignmentPRN.Tests;
public class VivaBudgetRulesTests
{
    [Fact] public void Question_deadline_never_exceeds_slot_end() {
        var now = new DateTime(2030, 1, 1, 10, 0, 0);
        Assert.Equal(now.AddSeconds(30), VivaBudgetRules.QuestionDeadline(now, now.AddSeconds(30), new(120, 2)));
        Assert.Equal(now.AddSeconds(120), VivaBudgetRules.QuestionDeadline(now, now.AddMinutes(10), new(120, 2)));
    }
    [Theory]
    [InlineData(0, 0, 1, true)]
    [InlineData(2, 2, 1, false)]
    [InlineData(0, 5, 1, false)]
    [InlineData(0, 0, 0, false)]
    [InlineData(-1, 0, 1, false)]
    public void Followup_requires_remaining_time_and_both_budgets(int perQuestion, int total, int secondsRemaining, bool expected) {
        var now = new DateTime(2030, 1, 1, 10, 0, 0);
        Assert.Equal(expected, VivaBudgetRules.CanAskFollowUp(perQuestion, total, 5, new(120, 2), now, now.AddSeconds(secondsRemaining), now.AddHours(1)));
    }
}
