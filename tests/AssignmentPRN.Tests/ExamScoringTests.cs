using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class ExamScoringTests
{
    [Fact]
    public void A_follow_up_weighs_half_a_main_question()
    {
        Assert.Equal(1m, ExamScoring.Weight(isFollowUp: false));
        Assert.Equal(0.5m, ExamScoring.Weight(isFollowUp: true));
    }

    [Fact]
    public void Main_questions_only_score_like_a_plain_percentage()
    {
        var score = ExamScoring.Score([(false, true), (false, true), (false, false), (false, false)]);

        Assert.Equal(5.0m, score);
    }

    [Fact]
    public void A_right_follow_up_lifts_the_score_less_than_a_main_question()
    {
        // 2 main right + 1 main wrong + 1 follow-up right = 2.5 / 3.5 of 10.
        var score = ExamScoring.Score([(false, true), (false, true), (false, false), (true, true)]);

        Assert.Equal(7.1m, score);
    }

    [Fact]
    public void Everything_right_is_ten()
    {
        Assert.Equal(10m, ExamScoring.Score([(false, true), (true, true)]));
    }

    [Fact]
    public void An_empty_paper_scores_zero()
    {
        Assert.Equal(0m, ExamScoring.Score([]));
    }
}
