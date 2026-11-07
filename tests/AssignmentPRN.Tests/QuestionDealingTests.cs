using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

/// <summary>
/// The randomiser behind "mỗi sinh viên một đề". Everything here runs without a
/// database, so the no-repeat rule is checked on its own.
/// </summary>
public class QuestionDealingTests
{
    private static IReadOnlyList<int> Pool(int size) => Enumerable.Range(1, size).ToList();

    [Fact]
    public void Pick_returns_the_requested_number_of_questions()
    {
        var picked = QuestionService.PickQuestions(Pool(20), 5, random: new Random(1));

        Assert.Equal(5, picked.Count);
    }

    [Fact]
    public void Pick_never_repeats_a_question_inside_one_paper()
    {
        var picked = QuestionService.PickQuestions(Pool(20), 10, random: new Random(7));

        Assert.Equal(picked.Count, picked.Distinct().Count());
    }

    [Fact]
    public void Pick_skips_questions_already_handed_out()
    {
        var taken = new[] { 1, 2, 3, 4, 5 };

        var picked = QuestionService.PickQuestions(Pool(10), 5, taken, new Random(3));

        Assert.Equal(5, picked.Count);
        Assert.DoesNotContain(picked, id => taken.Contains(id));
    }

    [Fact]
    public void Pick_returns_what_is_left_when_the_pool_runs_dry()
    {
        // Callers read Count to detect the thin bank, so a short result must not throw.
        var picked = QuestionService.PickQuestions(Pool(3), 10, random: new Random(5));

        Assert.Equal(3, picked.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Pick_returns_nothing_for_a_non_positive_count(int count)
    {
        Assert.Empty(QuestionService.PickQuestions(Pool(10), count, random: new Random(9)));
    }

    [Fact]
    public void Pick_shuffles_rather_than_taking_the_first_questions()
    {
        // A picker that ignored the shuffle would return 1..5 for a sorted pool.
        var picked = QuestionService.PickQuestions(Pool(200), 5, random: new Random(11));

        Assert.NotEqual(new[] { 1, 2, 3, 4, 5 }, picked);
    }

    /// <summary>
    /// The rule from the spec: consecutive students of one exam never share a question.
    /// This is exactly how <c>QuestionService.AssignToExamAsync</c> drives the picker.
    /// </summary>
    [Fact]
    public void Dealing_a_whole_exam_gives_every_student_a_different_paper()
    {
        const int studentCount = 8;
        const int perStudent = 5;
        var pool = Pool(studentCount * perStudent);
        var taken = new HashSet<int>();
        var papers = new List<IReadOnlyList<int>>();

        for (var student = 0; student < studentCount; student++)
        {
            var paper = QuestionService.PickQuestions(pool, perStudent, taken, new Random(student));
            Assert.Equal(perStudent, paper.Count);
            foreach (var questionId in paper)
            {
                taken.Add(questionId);
            }

            papers.Add(paper);
        }

        var everyQuestion = papers.SelectMany(paper => paper).ToList();
        Assert.Equal(studentCount * perStudent, everyQuestion.Count);
        Assert.Equal(everyQuestion.Count, everyQuestion.Distinct().Count());
    }

    [Fact]
    public void CountAvailable_reports_the_questions_still_free()
    {
        Assert.Equal(7, QuestionService.CountAvailableQuestions(Pool(10), new[] { 1, 2, 3 }));
    }

    [Fact]
    public void CountAvailable_ignores_taken_ids_outside_the_pool()
    {
        Assert.Equal(5, QuestionService.CountAvailableQuestions(Pool(5), new[] { 99, 100 }));
    }
}
