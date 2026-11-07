using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class FollowUpRoundTests
{
    private const int Topic = 7;
    private const int OtherTopic = 8;

    private static FollowUpSource Answered(
        int slot,
        QuestionDifficulty difficulty,
        bool correct,
        int? topic = Topic) => new(slot, slot, topic, difficulty, correct);

    private static FollowUpCandidate Bank(int id, QuestionDifficulty difficulty, int topic = Topic) =>
        new(id, topic, difficulty);

    [Fact]
    public void A_wrong_answer_gets_an_easier_question_on_the_same_topic()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Medium, correct: false)],
            [Bank(10, QuestionDifficulty.Hard), Bank(11, QuestionDifficulty.Easy), Bank(12, QuestionDifficulty.Easy, OtherTopic)],
            maxCount: 1);

        Assert.Equal(new FollowUpPick(1, 11), Assert.Single(picks));
    }

    [Fact]
    public void A_right_answer_gets_a_harder_question()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Medium, correct: true)],
            [Bank(10, QuestionDifficulty.Easy), Bank(11, QuestionDifficulty.Medium), Bank(12, QuestionDifficulty.Hard)],
            maxCount: 1);

        Assert.Equal(12, Assert.Single(picks).QuestionId);
    }

    [Fact]
    public void The_same_level_is_used_when_nothing_further_exists()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Hard, correct: true)],
            [Bank(10, QuestionDifficulty.Easy), Bank(11, QuestionDifficulty.Hard)],
            maxCount: 1);

        Assert.Equal(11, Assert.Single(picks).QuestionId);
    }

    [Fact]
    public void A_wrong_answer_never_gets_a_harder_question()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false)],
            [Bank(10, QuestionDifficulty.Medium), Bank(11, QuestionDifficulty.Hard)],
            maxCount: 1);

        Assert.Empty(picks);
    }

    [Fact]
    public void Wrong_answers_are_served_before_right_ones_and_the_cap_holds()
    {
        var picks = QuestionService.PlanFollowUps(
            [
                Answered(1, QuestionDifficulty.Medium, correct: true),
                Answered(2, QuestionDifficulty.Medium, correct: false),
                Answered(3, QuestionDifficulty.Medium, correct: false)
            ],
            [
                Bank(10, QuestionDifficulty.Easy),
                Bank(11, QuestionDifficulty.Easy),
                Bank(12, QuestionDifficulty.Hard)
            ],
            maxCount: 2);

        Assert.Equal([2, 3], picks.Select(pick => pick.ParentExamQuestionId));
    }

    [Fact]
    public void A_bank_question_is_never_dealt_twice_to_one_student()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false), Answered(2, QuestionDifficulty.Easy, correct: false)],
            [Bank(10, QuestionDifficulty.Easy)],
            maxCount: 2);

        Assert.Equal(10, Assert.Single(picks).QuestionId);
    }

    [Fact]
    public void Questions_fresh_to_the_session_are_preferred()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false)],
            [Bank(10, QuestionDifficulty.Easy), Bank(11, QuestionDifficulty.Easy)],
            maxCount: 1,
            usedInSession: [10]);

        Assert.Equal(11, Assert.Single(picks).QuestionId);
    }

    [Fact]
    public void A_used_question_is_still_dealt_when_it_is_the_only_one()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false)],
            [Bank(10, QuestionDifficulty.Easy)],
            maxCount: 1,
            usedInSession: [10]);

        Assert.Equal(10, Assert.Single(picks).QuestionId);
    }

    [Fact]
    public void A_main_question_without_a_topic_is_skipped()
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false, topic: null)],
            [Bank(10, QuestionDifficulty.Easy)],
            maxCount: 1);

        Assert.Empty(picks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_follow_ups_when_the_session_allows_none(int maxCount)
    {
        var picks = QuestionService.PlanFollowUps(
            [Answered(1, QuestionDifficulty.Easy, correct: false)],
            [Bank(10, QuestionDifficulty.Easy)],
            maxCount);

        Assert.Empty(picks);
    }

    [Fact]
    public void An_empty_bank_yields_nothing()
    {
        Assert.Empty(QuestionService.PlanFollowUps([Answered(1, QuestionDifficulty.Easy, correct: false)], [], maxCount: 3));
    }

    [Fact]
    public void Difficulty_order_steps_away_from_the_main_level_then_falls_back_to_it()
    {
        Assert.Equal(
            [QuestionDifficulty.Medium, QuestionDifficulty.Easy, QuestionDifficulty.Hard],
            QuestionService.FollowUpDifficultyOrder(QuestionDifficulty.Hard, isCorrect: false));
        Assert.Equal(
            [QuestionDifficulty.Medium, QuestionDifficulty.Hard, QuestionDifficulty.Easy],
            QuestionService.FollowUpDifficultyOrder(QuestionDifficulty.Easy, isCorrect: true));
    }

    [Fact]
    public void A_round_opens_only_with_at_least_a_minute_left()
    {
        var end = new DateTime(2026, 10, 1, 9, 30, 0);

        Assert.True(QuestionService.CanOpenFollowUpRound(end.AddMinutes(-1), end));
        Assert.False(QuestionService.CanOpenFollowUpRound(end.AddSeconds(-59), end));
        Assert.False(QuestionService.CanOpenFollowUpRound(end.AddMinutes(1), end));
    }
}
