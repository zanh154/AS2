using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class ExamResultResponseTests
{
    [Fact]
    public void Summary_SeparatesCorrectIncorrectAndUnansweredAnswers()
    {
        var result = new ExamResultResponse
        {
            Questions =
            [
                Question(selectedOptionId: 1, correctOptionId: 1),
                Question(selectedOptionId: 2, correctOptionId: 1),
                Question(selectedOptionId: null, correctOptionId: 1)
            ]
        };

        Assert.Equal(3, result.TotalQuestions);
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(1, result.IncorrectCount);
        Assert.Equal(1, result.UnansweredCount);
        Assert.Equal(3.3m, result.Score);
    }

    [Fact]
    public void Score_RoundsHalfAwayFromZeroToOneDecimalPlace()
    {
        var result = new ExamResultResponse
        {
            Questions =
            [
                Question(1, 1),
                Question(1, 1),
                Question(2, 1),
                Question(2, 1)
            ]
        };

        Assert.Equal(5.0m, result.Score);
    }

    [Fact]
    public void A_follow_up_counts_half_in_the_score_and_is_listed_apart()
    {
        var result = new ExamResultResponse
        {
            Questions =
            [
                Question(1, 1),
                Question(2, 1),
                Question(1, 1, parentExamQuestionId: 1)
            ]
        };

        // 1 main right + 1 main wrong + 1 follow-up right = 1.5 / 2.5 of 10.
        Assert.Equal(6.0m, result.Score);
        Assert.Equal(2, result.MainQuestions.Count);
        Assert.Single(result.FollowUpQuestions);
        Assert.Equal(1, result.FollowUpCorrectCount);
    }

    [Fact]
    public void The_room_is_in_the_follow_up_round_once_a_follow_up_exists()
    {
        var mainOnly = new ExamRoomResponse { Questions = [RoomQuestion(1), RoomQuestion(2)] };
        Assert.False(mainOnly.IsFollowUpRound);
        Assert.Equal(2, mainOnly.OpenRound.Count);
        Assert.Empty(mainOnly.SubmittedRound);

        var secondRound = new ExamRoomResponse
        {
            Questions = [RoomQuestion(1), RoomQuestion(2), RoomQuestion(3, parent: 1)]
        };
        Assert.True(secondRound.IsFollowUpRound);
        Assert.Equal(3, Assert.Single(secondRound.OpenRound).ExamQuestionId);
        Assert.Equal([1, 2], secondRound.SubmittedRound.Select(question => question.ExamQuestionId));
    }

    private static ExamRoomQuestion RoomQuestion(int id, int? parent = null) => new()
    {
        ExamQuestionId = id,
        OrderNo = id,
        ParentExamQuestionId = parent
    };

    private static ExamResultQuestion Question(
        int? selectedOptionId,
        int correctOptionId,
        int? parentExamQuestionId = null) => new()
    {
        SelectedOptionId = selectedOptionId,
        ParentExamQuestionId = parentExamQuestionId,
        Options =
        [
            new ExamResultOption { OptionId = 1, Text = "A", IsCorrect = correctOptionId == 1 },
            new ExamResultOption { OptionId = 2, Text = "B", IsCorrect = correctOptionId == 2 }
        ]
    };
}
