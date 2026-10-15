using System.Text.Json;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;

namespace AssignmentPRN.Tests;

/// <summary>
/// What the analyzer accepts from the model. A viva is running while this parses, so
/// anything doubtful has to end as "no probe" rather than as an exception or a junk
/// question read aloud to the student.
/// </summary>
public class FollowUpAnalyzerTests
{
    private static FollowUpDecision Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return OllamaFollowUpAnalyzer.Parse(document);
    }

    [Fact]
    public void Missing_point_becomes_a_probe()
    {
        var decision = Parse("""
            {"needsFollowUp": true, "reason": "MissingPoint", "question": "Bạn hãy nói rõ cơ chế khoá ở đây?"}
            """);

        Assert.True(decision.NeedsFollowUp);
        Assert.Equal(FollowUpReason.MissingPoint, decision.Reason);
        Assert.Equal("Bạn hãy nói rõ cơ chế khoá ở đây?", decision.Question);
    }

    [Theory]
    [InlineData("Vague", FollowUpReason.Vague)]
    [InlineData("unclear", FollowUpReason.Vague)]
    [InlineData("Contradictory", FollowUpReason.Contradictory)]
    [InlineData("missing_point", FollowUpReason.MissingPoint)]
    public void Reason_names_are_read_leniently(string reason, FollowUpReason expected)
    {
        var decision = Parse($$"""
            {"needsFollowUp": true, "reason": "{{reason}}", "question": "Ý đó cụ thể là gì?"}
            """);

        Assert.Equal(expected, decision.Reason);
    }

    [Fact]
    public void Sufficient_answer_is_not_probed()
    {
        var decision = Parse("""
            {"needsFollowUp": false, "reason": "Sufficient", "question": ""}
            """);

        Assert.False(decision.NeedsFollowUp);
        Assert.Equal(FollowUpDecision.None, decision);
    }

    [Fact]
    public void A_reason_without_the_flag_is_still_refused_when_the_flag_says_no()
    {
        // The model contradicted itself. Taking it at its word costs the student nothing.
        var decision = Parse("""
            {"needsFollowUp": false, "reason": "Vague", "question": "Bạn nói rõ hơn được không?"}
            """);

        Assert.False(decision.NeedsFollowUp);
    }

    [Fact]
    public void An_unknown_reason_means_no_probe()
    {
        var decision = Parse("""
            {"needsFollowUp": true, "reason": "Perfect", "question": "Thế còn gì nữa?"}
            """);

        Assert.False(decision.NeedsFollowUp);
    }

    [Fact]
    public void An_empty_question_means_no_probe()
    {
        var decision = Parse("""
            {"needsFollowUp": true, "reason": "Vague", "question": "   "}
            """);

        Assert.False(decision.NeedsFollowUp);
    }

    [Fact]
    public void A_question_longer_than_the_limit_is_dropped()
    {
        // Past this length the model has stopped asking and started lecturing; reading
        // that aloud would eat the student's answering time.
        var tooLong = new string('a', OllamaFollowUpAnalyzer.MaxQuestionLength) + "?";
        var decision = Parse($$"""
            {"needsFollowUp": true, "reason": "Vague", "question": "{{tooLong}}"}
            """);

        Assert.False(decision.NeedsFollowUp);
    }

    [Fact]
    public void A_question_at_exactly_the_limit_is_kept()
    {
        var exact = new string('a', OllamaFollowUpAnalyzer.MaxQuestionLength - 1) + "?";
        var decision = Parse($$"""
            {"needsFollowUp": true, "reason": "Vague", "question": "{{exact}}"}
            """);

        Assert.True(decision.NeedsFollowUp);
    }

    [Fact]
    public void A_sentence_that_is_not_a_question_is_dropped()
    {
        // Lỗi thật đã đo được: model viết một câu dành cho giảng viên đọc, không phải
        // câu hỏi cho sinh viên. Câu đó sẽ được đọc lên trong buổi thi, nên bỏ.
        var decision = Parse("""
            {"needsFollowUp": true, "reason": "Vague", "question": "Giảng viên nên hỏi thêm về cơ chế khoá."}
            """);

        Assert.False(decision.NeedsFollowUp);
    }

    [Fact]
    public void A_question_is_trimmed()
    {
        var decision = Parse("""
            {"needsFollowUp": true, "reason": "Vague", "question": "  Vì sao?  "}
            """);

        Assert.Equal("Vì sao?", decision.Question);
    }

    [Theory]
    [InlineData("""{"reason": "Vague"}""")]
    [InlineData("""{"needsFollowUp": true, "question": "Vì sao?"}""")]
    [InlineData("""{"needsFollowUp": true, "reason": "Vague", "question": 42}""")]
    [InlineData("""{"needsFollowUp": true, "reason": 1, "question": "Vì sao?"}""")]
    [InlineData("[]")]
    [InlineData("\"Sufficient\"")]
    public void Anything_malformed_means_no_probe(string json)
    {
        Assert.False(Parse(json).NeedsFollowUp);
    }

    [Fact]
    public async Task A_blank_transcript_is_never_sent_to_the_model()
    {
        // Nothing was said, so there is nothing to probe; this also keeps the call off
        // the critical path when a student stays silent.
        var analyzer = new OllamaFollowUpAnalyzer(null!);
        var context = new FollowUpContext("Câu hỏi?", null, [], "   ", []);

        Assert.Equal(FollowUpDecision.None, await analyzer.AnalyzeAsync(context));
    }
}
