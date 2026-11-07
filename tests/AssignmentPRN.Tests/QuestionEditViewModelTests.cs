using System.ComponentModel.DataAnnotations;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Presentation.ViewModels;

namespace AssignmentPRN.Tests;

/// <summary>
/// The question editor marks its correct answer with a radio group, which posts one
/// value for the whole group rather than a flag per row. These tests pin that the
/// check reads the posted index and that it has to point at a row with text.
/// </summary>
public class QuestionEditViewModelTests
{
    private static QuestionEditViewModel Model(int correctIndex, params string[] options) => new()
    {
        CourseId = 1,
        QuestionText = "Câu hỏi thử",
        CorrectIndex = correctIndex,
        Options = options.Select(text => new QuestionOptionViewModel { Text = text }).ToList()
    };

    private static IReadOnlyList<ValidationResult> Validate(QuestionEditViewModel model) =>
        model.Validate(new ValidationContext(model)).ToList();

    [Fact]
    public void A_question_with_a_picked_answer_passes()
    {
        Assert.Empty(Validate(Model(1, "Sai", "Đúng", "Sai", "Sai")));
    }

    [Fact]
    public void The_last_row_may_be_the_answer()
    {
        Assert.Empty(Validate(Model(3, "A", "B", "C", "D")));
    }

    [Fact]
    public void Not_picking_an_answer_is_rejected()
    {
        var errors = Validate(Model(-1, "A", "B", "C", "D"));

        Assert.Contains(nameof(QuestionEditViewModel.CorrectIndex), Assert.Single(errors).MemberNames);
    }

    [Fact]
    public void Picking_an_empty_row_is_rejected()
    {
        // Ticking C then clearing its text must not slip through as a valid question.
        var errors = Validate(Model(2, "A", "B", string.Empty, string.Empty));

        Assert.Contains(nameof(QuestionEditViewModel.CorrectIndex), Assert.Single(errors).MemberNames);
    }

    [Fact]
    public void An_index_beyond_the_rows_is_rejected()
    {
        Assert.Single(Validate(Model(9, "A", "B", string.Empty, string.Empty)));
    }

    [Fact]
    public void Fewer_than_two_filled_options_is_rejected()
    {
        var errors = Validate(Model(0, "Chỉ một phương án", string.Empty, string.Empty, string.Empty));

        Assert.Contains(nameof(QuestionEditViewModel.Options), Assert.Single(errors).MemberNames);
    }

    [Fact]
    public void A_missing_course_is_reported()
    {
        var model = Model(0, "A", "B");
        model.CourseId = 0;

        Assert.Contains(
            Validate(model),
            error => error.MemberNames.Contains(nameof(QuestionEditViewModel.CourseId)));
    }

    [Fact]
    public void A_follow_up_without_a_topic_is_rejected()
    {
        var model = Model(0, "A", "B");
        model.QuestionType = QuestionType.FollowUp;

        Assert.Contains(nameof(QuestionEditViewModel.MaterialId), Assert.Single(Validate(model)).MemberNames);
    }

    [Fact]
    public void A_follow_up_with_a_topic_passes()
    {
        var model = Model(0, "A", "B");
        model.QuestionType = QuestionType.FollowUp;
        model.MaterialId = 5;

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void A_main_question_does_not_need_a_topic()
    {
        Assert.Empty(Validate(Model(0, "A", "B")));
    }

    [Fact]
    public void BuildOptions_returns_the_number_of_rows_the_form_binds_to()
    {
        var options = QuestionEditViewModel.BuildOptions();

        Assert.Equal(QuestionEditViewModel.OptionRows, options.Count);
        Assert.All(options, option => Assert.Equal(string.Empty, option.Text));
    }
}
