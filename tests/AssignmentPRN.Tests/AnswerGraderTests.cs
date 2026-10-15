using System.Text.Json;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;

namespace AssignmentPRN.Tests;

/// <summary>
/// What the grader takes from the model. Marks that cannot be read are left out rather
/// than guessed at: RubricScoring then shows the examiner that the criterion was never
/// marked, which is honest, where a guessed zero would not be.
/// </summary>
public class AnswerGraderTests
{
    private static AnswerGrade Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return OllamaAnswerGrader.Parse(document);
    }

    [Fact]
    public void A_full_reply_is_read()
    {
        var grade = Parse("""
            {"criteria": [{"criteriaId": 1, "score": 5.5, "comment": "Nêu đúng khái niệm"},
                          {"criteriaId": 2, "score": 2, "comment": "Ví dụ còn sơ sài"}],
             "strengths": "Nắm khái niệm", "weaknesses": "Thiếu ví dụ",
             "missingPoints": "Chưa nói về chi phí", "confidence": 0.8}
            """);

        Assert.Equal(2, grade.Marks.Count);
        Assert.Equal(5.5m, grade.Marks[0].Score);
        Assert.Equal("Ví dụ còn sơ sài", grade.Marks[1].Comment);
        Assert.Equal("Nắm khái niệm", grade.Strengths);
        Assert.Equal("Thiếu ví dụ", grade.Weaknesses);
        Assert.Equal("Chưa nói về chi phí", grade.MissingPoints);
        Assert.Equal(0.8m, grade.Confidence);
    }

    [Fact]
    public void A_criterion_without_a_number_is_left_unmarked()
    {
        var grade = Parse("""
            {"criteria": [{"criteriaId": 1, "score": "sáu", "comment": "x"},
                          {"criteriaId": 2, "score": 4}]}
            """);

        var mark = Assert.Single(grade.Marks);
        Assert.Equal(2, mark.CriteriaId);
    }

    [Fact]
    public void A_criterion_without_an_id_is_left_out()
    {
        var grade = Parse("""{"criteria": [{"score": 4, "comment": "x"}]}""");

        Assert.Empty(grade.Marks);
    }

    [Fact]
    public void Out_of_range_marks_are_still_read_and_clamped_later()
    {
        // Clamping belongs to the rubric, which knows each criterion's maximum.
        var grade = Parse("""{"criteria": [{"criteriaId": 1, "score": 99}, {"criteriaId": 2, "score": -4}]}""");

        Assert.Equal(99m, grade.Marks[0].Score);
        Assert.Equal(-4m, grade.Marks[1].Score);
    }

    [Fact]
    public void An_absurd_number_is_refused_rather_than_clamped()
    {
        var grade = Parse("""{"criteria": [{"criteriaId": 1, "score": 1e20}, {"criteriaId": 2, "score": 3}]}""");

        var mark = Assert.Single(grade.Marks);
        Assert.Equal(2, mark.CriteriaId);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.2")]
    [InlineData("\"cao\"")]
    public void A_confidence_outside_zero_to_one_is_dropped(string value)
    {
        var grade = Parse($$"""{"criteria": [], "confidence": {{value}}}""");

        Assert.Null(grade.Confidence);
    }

    [Fact]
    public void Notes_are_trimmed_and_blank_ones_become_nothing()
    {
        var grade = Parse("""
            {"criteria": [], "strengths": "  tốt  ", "weaknesses": "   ", "missingPoints": ""}
            """);

        Assert.Equal("tốt", grade.Strengths);
        Assert.Null(grade.Weaknesses);
        Assert.Null(grade.MissingPoints);
    }

    [Fact]
    public void A_note_longer_than_the_limit_is_cut()
    {
        var grade = Parse($$"""{"criteria": [], "strengths": "{{new string('a', 3000)}}"}""");

        Assert.Equal(OllamaAnswerGrader.MaxNoteLength, grade.Strengths!.Length);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"nope\"")]
    [InlineData("""{"criteria": "none"}""")]
    [InlineData("""{"criteria": ["not an object"]}""")]
    public void Anything_malformed_yields_no_marks(string json)
    {
        Assert.Empty(Parse(json).Marks);
    }

    [Fact]
    public async Task An_answer_with_no_rubric_is_not_sent_to_the_model()
    {
        // There is nothing to mark against, so the examiner marks this one by hand.
        var grader = new OllamaAnswerGrader(null!);
        var context = new GradingContext("Câu hỏi?", null, [], "Em trả lời.", []);

        Assert.Equal(AnswerGrade.None, await grader.GradeAsync(context));
    }

    [Fact]
    public async Task A_silent_candidate_is_not_sent_to_the_model()
    {
        var grader = new OllamaAnswerGrader(null!);
        var context = new GradingContext("Câu hỏi?", null,
            [new RubricCriterionInfo(1, "Tiêu chí", null, 10m)], "  ",
            [new FollowUpExchange("Hỏi thêm?", null)]);

        Assert.Equal(AnswerGrade.None, await grader.GradeAsync(context));
    }

    [Fact]
    public async Task A_reply_to_a_probe_alone_is_still_worth_grading()
    {
        // The student said nothing to the question but answered the probe; that is an
        // answer, and refusing to grade it would hand the examiner a blank.
        var grader = new OllamaAnswerGrader(new OllamaJsonClient(
            new ThrowingHttpClientFactory(), new StubOptions(), new NullLogger<OllamaJsonClient>()));
        var context = new GradingContext("Câu hỏi?", null,
            [new RubricCriterionInfo(1, "Tiêu chí", null, 10m)], null,
            [new FollowUpExchange("Hỏi thêm?", "Em nói ở đây.")]);

        // The model is unreachable, so nothing comes back — but it was asked.
        Assert.Equal(AnswerGrade.None, await grader.GradeAsync(context));
        Assert.True(ThrowingHttpClientFactory.Called);
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public static bool Called { get; private set; }

        public HttpClient CreateClient(string name)
        {
            Called = true;
            return new HttpClient(new ThrowingHandler());
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromException<HttpResponseMessage>(new HttpRequestException("no model here"));
        }
    }

    private sealed class StubOptions : Microsoft.Extensions.Options.IOptions<AssignmentPRN.Business.OllamaOptions>
    {
        public AssignmentPRN.Business.OllamaOptions Value { get; } = new();
    }

    private sealed class NullLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    [Theory]
    // Đúng câu đã đo được từ model.
    [InlineData("Em mất điểm ở tiêu chí 87 và 99.", "Em mất điểm ở tiêu chí tương ứng.")]
    [InlineData("Chưa đạt criteriaId 12.", "Chưa đạt tiêu chí tương ứng.")]
    [InlineData("Thiếu ý ở tiêu chí: 5, 6, 7.", "Thiếu ý ở tiêu chí tương ứng.")]
    // Tên tiêu chí thì giữ nguyên; chỉ mã mới bị bỏ.
    [InlineData("Đạt tiêu chí Đúng khái niệm.", "Đạt tiêu chí Đúng khái niệm.")]
    [InlineData("Trả lời tốt, nêu được 3 ví dụ.", "Trả lời tốt, nêu được 3 ví dụ.")]
    public void A_note_never_carries_a_criteria_id_to_the_reader(string note, string expected) =>
        Assert.Equal(expected, OllamaAnswerGrader.Scrub(note));

    [Fact]
    public void The_criteria_id_is_scrubbed_from_every_note_the_model_writes()
    {
        var grade = Parse("""
            {"criteria": [{"criteriaId": 1, "score": 2, "comment": "Mất điểm ở tiêu chí 87."}],
             "strengths": "Tốt ở tiêu chí 87", "weaknesses": "Yếu ở criteriaId 99",
             "missingPoints": "Thiếu ý tiêu chí 87 và 99", "confidence": 0.8}
            """);

        Assert.DoesNotContain("87", Assert.Single(grade.Marks).Comment);
        Assert.DoesNotContain("87", grade.Strengths);
        Assert.DoesNotContain("99", grade.Weaknesses);
        Assert.DoesNotContain("99", grade.MissingPoints);
    }
}
