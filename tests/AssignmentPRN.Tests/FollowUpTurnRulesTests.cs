using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

/// <summary>
/// The budget around a probe. These rules decide whether a student gets asked one more
/// thing, so every "no" here has to be a deliberate one: the alternative is a question
/// read aloud with no time left to answer it.
/// </summary>
public class FollowUpTurnRulesTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 10, 0, 0);
    private static readonly ExamVivaSettings Settings = new(SecondsPerQuestion: 120, MaxFollowUpsPerQuestion: 2);

    private static bool CanAsk(
        int askedForQuestion = 0,
        int askedInSlot = 0,
        int slotLimit = 5,
        ExamVivaSettings? settings = null,
        int secondsToQuestionDeadline = 120,
        int secondsToSlotEnd = 600) =>
        FollowUpTurnRules.CanAsk(
            askedForQuestion, askedInSlot, slotLimit, settings ?? Settings, Now,
            Now.AddSeconds(secondsToQuestionDeadline), Now.AddSeconds(secondsToSlotEnd));

    [Fact]
    public void A_fresh_question_with_time_left_may_be_probed()
    {
        Assert.True(CanAsk());
    }

    [Fact]
    public void The_per_question_budget_is_respected()
    {
        Assert.True(CanAsk(askedForQuestion: 1));
        Assert.False(CanAsk(askedForQuestion: 2));
        Assert.False(CanAsk(askedForQuestion: 3));
    }

    [Fact]
    public void The_slot_budget_is_respected()
    {
        Assert.True(CanAsk(askedInSlot: 4, slotLimit: 5));
        Assert.False(CanAsk(askedInSlot: 5, slotLimit: 5));
    }

    [Fact]
    public void A_session_configured_for_no_follow_ups_never_probes()
    {
        Assert.False(CanAsk(settings: new(SecondsPerQuestion: 120, MaxFollowUpsPerQuestion: 0)));
    }

    [Fact]
    public void A_slot_limit_of_zero_never_probes()
    {
        Assert.False(CanAsk(slotLimit: 0));
    }

    [Fact]
    public void A_probe_needs_enough_time_to_be_read_and_answered()
    {
        // Exactly at the minimum is still worth asking; a second under is not.
        var seconds = (int)FollowUpTurnRules.MinimumTimeToProbe.TotalSeconds;
        Assert.True(CanAsk(secondsToQuestionDeadline: seconds));
        Assert.False(CanAsk(secondsToQuestionDeadline: seconds - 1));
    }

    [Fact]
    public void The_end_of_the_slot_closes_probing_even_with_question_time_left()
    {
        // The student's slot is the hard bound: the next candidate is waiting.
        Assert.False(CanAsk(secondsToQuestionDeadline: 120, secondsToSlotEnd: 10));
    }

    [Fact]
    public void A_question_whose_time_is_already_up_is_not_probed()
    {
        Assert.False(CanAsk(secondsToQuestionDeadline: 0));
        Assert.False(CanAsk(secondsToQuestionDeadline: -30));
    }

    [Fact]
    public void Counts_that_went_negative_are_refused_rather_than_trusted()
    {
        Assert.False(CanAsk(askedForQuestion: -1));
        Assert.False(CanAsk(askedInSlot: -1));
    }

    [Fact]
    public void A_probe_never_buys_time_past_the_question()
    {
        var deadline = FollowUpTurnRules.Deadline(Now, Now.AddSeconds(60), Now.AddSeconds(600));

        Assert.Equal(Now.AddSeconds(60), deadline);
    }

    [Fact]
    public void A_probe_never_runs_past_the_slot()
    {
        var deadline = FollowUpTurnRules.Deadline(Now, Now.AddSeconds(600), Now.AddSeconds(60));

        Assert.Equal(Now.AddSeconds(60), deadline);
    }

    [Fact]
    public void A_deadline_already_behind_us_is_clamped_to_now()
    {
        // Callers subtract this from now; a negative span would read as "time left".
        Assert.Equal(Now, FollowUpTurnRules.Deadline(Now, Now.AddSeconds(-30), Now.AddSeconds(600)));
    }

    [Theory]
    [InlineData(0, 0, 5, 2)]
    [InlineData(1, 0, 5, 1)]
    [InlineData(2, 0, 5, 0)]
    [InlineData(0, 4, 5, 1)]
    [InlineData(0, 5, 5, 0)]
    [InlineData(3, 9, 5, 0)]
    public void Remaining_counts_both_budgets_and_never_goes_negative(
        int askedForQuestion, int askedInSlot, int slotLimit, int expected)
    {
        Assert.Equal(expected, FollowUpTurnRules.Remaining(askedForQuestion, askedInSlot, slotLimit, Settings));
    }
}
