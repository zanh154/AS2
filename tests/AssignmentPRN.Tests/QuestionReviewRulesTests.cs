using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using DaEnums = AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Tests;

/// <summary>
/// The lifecycle rules behind the AI review desk: a manual question is trusted and
/// approved at once, an AI question waits, and a settled question can no longer be decided.
/// </summary>
public class QuestionReviewRulesTests
{
    [Theory]
    [InlineData(QuestionStatus.Draft, true)]
    [InlineData(QuestionStatus.PendingReview, true)]
    [InlineData(QuestionStatus.Approved, false)]
    [InlineData(QuestionStatus.Rejected, false)]
    [InlineData(QuestionStatus.Archived, false)]
    public void Only_a_waiting_question_can_be_decided(QuestionStatus status, bool decidable)
    {
        Assert.Equal(decidable, QuestionReviewService.IsDecidable(status));
    }

    [Theory]
    [InlineData(DaEnums.QuestionSource.Manual, DaEnums.QuestionStatus.Approved)]
    [InlineData(DaEnums.QuestionSource.AI, DaEnums.QuestionStatus.PendingReview)]
    public void A_new_question_is_stored_by_its_source(
        DaEnums.QuestionSource source,
        DaEnums.QuestionStatus expected)
    {
        Assert.Equal(expected, DaEnums.QuestionRules.InitialStatus(source));
    }

    [Fact]
    public void Only_an_ai_question_requires_review()
    {
        Assert.False(DaEnums.QuestionRules.RequiresReview(DaEnums.QuestionSource.Manual));
        Assert.True(DaEnums.QuestionRules.RequiresReview(DaEnums.QuestionSource.AI));
    }

    [Fact]
    public void Only_an_approved_question_can_be_dealt()
    {
        Assert.True(DaEnums.QuestionRules.IsSelectable(DaEnums.QuestionStatus.Approved));
        Assert.False(DaEnums.QuestionRules.IsSelectable(DaEnums.QuestionStatus.PendingReview));
    }
}
