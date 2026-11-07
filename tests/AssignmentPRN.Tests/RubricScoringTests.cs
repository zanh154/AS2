using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

/// <summary>
/// What the model is allowed to do to a student's grade. Every case here is something a
/// language model actually does — scoring above the maximum, inventing a criterion,
/// skipping one in silence — and each has to land somewhere defensible.
/// </summary>
public class RubricScoringTests
{
    private static readonly IReadOnlyList<RubricCriterionInfo> Rubric =
    [
        new(1, "Đúng khái niệm", null, 6m),
        new(2, "Ví dụ minh hoạ", null, 4m)
    ];

    [Fact]
    public void A_full_mark_on_every_criterion_is_ten()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, "tốt"), new(2, 4m, "rõ")]);

        Assert.Equal(10m, scored.Score);
        Assert.All(scored.Criteria, criterion => Assert.True(criterion.Marked));
        Assert.Equal(0, scored.UnmarkedCount);
    }

    [Fact]
    public void Marks_are_scaled_to_ten_by_the_weight_of_each_criterion()
    {
        // 3 of 6 and 1 of 4 is 4 of 10.
        var scored = RubricScoring.Apply(Rubric, [new(1, 3m, null), new(2, 1m, null)]);

        Assert.Equal(4m, scored.Score);
    }

    [Fact]
    public void A_mark_above_the_criterion_maximum_is_clamped()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 99m, null), new(2, 4m, null)]);

        Assert.Equal(6m, scored.Criteria[0].Awarded);
        Assert.Equal(10m, scored.Score);
    }

    [Fact]
    public void A_negative_mark_is_clamped_to_zero()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, -5m, null), new(2, 4m, null)]);

        Assert.Equal(0m, scored.Criteria[0].Awarded);
        Assert.Equal(4m, scored.Score);
    }

    [Fact]
    public void A_criterion_the_model_skipped_scores_zero_and_says_so()
    {
        // A silent zero would look like a judgement the examiner can trust.
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, "tốt")]);

        var skipped = scored.Criteria[1];
        Assert.False(skipped.Marked);
        Assert.Equal(0m, skipped.Awarded);
        Assert.Equal(RubricScoring.NotMarkedComment, skipped.Comment);
        Assert.Equal(1, scored.UnmarkedCount);
        Assert.Equal(6m, scored.Score);
    }

    [Fact]
    public void A_criterion_that_is_not_in_the_rubric_is_dropped()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, null), new(2, 4m, null), new(999, 10m, "bịa")]);

        Assert.Equal(2, scored.Criteria.Count);
        Assert.Equal(10m, scored.Score);
    }

    [Fact]
    public void A_criterion_marked_twice_keeps_the_first_mark()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, "lần đầu"), new(1, 0m, "lần sau"), new(2, 4m, null)]);

        Assert.Equal(6m, scored.Criteria[0].Awarded);
        Assert.Equal("lần đầu", scored.Criteria[0].Comment);
    }

    [Fact]
    public void Every_criterion_of_the_rubric_comes_back_in_order()
    {
        var scored = RubricScoring.Apply(Rubric, [new(2, 4m, null)]);

        Assert.Equal([1, 2], scored.Criteria.Select(x => x.CriteriaId));
    }

    [Fact]
    public void An_empty_rubric_cannot_produce_a_score()
    {
        // Zero out of zero is not a zero the student earned.
        var scored = RubricScoring.Apply([], []);

        Assert.Null(scored.Score);
        Assert.Empty(scored.Criteria);
    }

    [Fact]
    public void A_rubric_worth_nothing_cannot_produce_a_score()
    {
        var scored = RubricScoring.Apply([new(1, "Trống", null, 0m)], [new(1, 0m, null)]);

        Assert.Null(scored.Score);
    }

    [Fact]
    public void A_criterion_with_a_negative_maximum_is_treated_as_worth_nothing()
    {
        var scored = RubricScoring.Apply([new(1, "Hỏng", null, -4m), new(2, "Tốt", null, 10m)], [new(2, 5m, null)]);

        Assert.Equal(0m, scored.Criteria[0].MaxScore);
        Assert.Equal(5m, scored.Score);
    }

    [Fact]
    public void Scores_are_rounded_to_one_decimal_away_from_zero()
    {
        // 1 of 3 scaled to ten is 3.333..., and 2 of 3 is 6.666...
        var thirds = new List<RubricCriterionInfo> { new(1, "Một phần ba", null, 3m) };
        Assert.Equal(3.3m, RubricScoring.Apply(thirds, [new(1, 1m, null)]).Score);
        Assert.Equal(6.7m, RubricScoring.Apply(thirds, [new(1, 2m, null)]).Score);

        // 0.75 of 2 is 3.75 out of ten, which rounds up rather than to even.
        var halves = new List<RubricCriterionInfo> { new(1, "Hai điểm", null, 2m) };
        Assert.Equal(3.8m, RubricScoring.Apply(halves, [new(1, 0.75m, null)]).Score);
    }

    [Fact]
    public void A_blank_comment_is_stored_as_nothing_rather_than_as_whitespace()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, "   "), new(2, 4m, " rõ ràng ")]);

        Assert.Null(scored.Criteria[0].Comment);
        Assert.Equal("rõ ràng", scored.Criteria[1].Comment);
    }

    [Fact]
    public void A_comment_longer_than_the_column_is_cut_rather_than_refused()
    {
        var scored = RubricScoring.Apply(Rubric, [new(1, 6m, new string('a', 1500)), new(2, 4m, null)]);

        Assert.Equal(1000, scored.Criteria[0].Comment!.Length);
    }

    [Fact]
    public void A_slot_weighs_a_probe_at_half_the_question_it_probes()
    {
        // 8 on the question and 4 on its probe: (8 + 0.5 x 4) / 1.5.
        Assert.Equal(6.7m, RubricScoring.Slot([(false, 8m), (true, 4m)]));
    }

    [Fact]
    public void An_unmarked_answer_is_left_out_of_the_slot_rather_than_counted_as_zero()
    {
        // The second question has no rubric. Counting it as zero would mark the student
        // down for something a lecturer did not write.
        Assert.Equal(8m, RubricScoring.Slot([(false, 8m), (false, null)]));
    }

    [Fact]
    public void A_slot_with_nothing_marked_has_no_score()
    {
        Assert.Null(RubricScoring.Slot([(false, null), (true, null)]));
        Assert.Null(RubricScoring.Slot([]));
    }

    [Fact]
    public void A_slot_of_probes_alone_still_scores()
    {
        Assert.Equal(5m, RubricScoring.Slot([(true, 5m), (true, 5m)]));
    }
}
