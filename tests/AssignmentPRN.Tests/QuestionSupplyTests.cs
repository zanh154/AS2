using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

public class QuestionSupplyTests
{
    [Fact]
    public void A_session_needs_one_distinct_question_per_student_per_main_question()
    {
        Assert.Equal(30, ExamSessionService.RequiredQuestionCount(candidateCount: 3, questionsPerCandidate: 10));
    }

    [Fact]
    public void A_bank_that_exactly_covers_the_session_is_accepted()
    {
        ExamSessionService.EnsureQuestionSupply(candidateCount: 3, questionsPerCandidate: 4, available: 12);
    }

    [Fact]
    public void A_bank_one_question_short_is_refused_with_both_numbers()
    {
        var error = Assert.Throws<BusinessValidationException>(
            () => ExamSessionService.EnsureQuestionSupply(candidateCount: 3, questionsPerCandidate: 10, available: 12));

        Assert.Contains("12", error.Message);
        Assert.Contains("30", error.Message);
    }

    [Fact]
    public void A_session_without_students_needs_nothing()
    {
        ExamSessionService.EnsureQuestionSupply(candidateCount: 0, questionsPerCandidate: 5, available: 0);
    }

    [Fact]
    public void Negative_inputs_never_turn_into_a_negative_requirement()
    {
        Assert.Equal(0, ExamSessionService.RequiredQuestionCount(candidateCount: -2, questionsPerCandidate: 5));
    }
}
