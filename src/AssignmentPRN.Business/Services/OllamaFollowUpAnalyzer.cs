using System.Text;
using System.Text.Json;
using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Reads a spoken answer against its question and rubric and decides whether a human
/// examiner would ask one more thing. Backed by the local model through
/// <see cref="OllamaJsonClient"/>, which never throws, so the worst case here is "no
/// probe" and the viva moves to the next question.
/// </summary>
public sealed class OllamaFollowUpAnalyzer(OllamaJsonClient client) : IFollowUpAnalyzer
{
    /// <summary>
    /// What the probe is allowed to be. Anything longer is a sign the model started
    /// explaining instead of asking, and is dropped.
    /// </summary>
    internal const int MaxQuestionLength = 300;

    /// <summary>
    /// A student is waiting in silence while this runs, so the budget is short. Past
    /// it the viva simply carries on.
    /// </summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    public async Task<FollowUpDecision> AnalyzeAsync(
        FollowUpContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Nothing was said: there is no transcript to probe, and asking "say more" would
        // only burn the student's remaining time.
        if (string.IsNullOrWhiteSpace(context.Transcript)) return FollowUpDecision.None;

        using var answer = await client.GenerateAsync(
            BuildPrompt(context), Schema, Budget, maxTokens: 400, cancellationToken);

        return answer is null ? FollowUpDecision.None : Parse(answer);
    }

    /// <summary>
    /// Turns the model's JSON into a decision, dropping anything unusable. Separated from
    /// the HTTP call so the rules can be exercised without a model running.
    /// </summary>
    internal static FollowUpDecision Parse(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return FollowUpDecision.None;

        var reason = root.TryGetProperty("reason", out var reasonValue) && reasonValue.ValueKind == JsonValueKind.String
            ? ReadReason(reasonValue.GetString())
            : FollowUpReason.Sufficient;
        if (reason == FollowUpReason.Sufficient) return FollowUpDecision.None;

        // A model that says "needsFollowUp: false" but still writes a reason is taken at
        // its word: no probe.
        if (root.TryGetProperty("needsFollowUp", out var needs)
            && needs.ValueKind == JsonValueKind.False)
        {
            return FollowUpDecision.None;
        }

        if (!root.TryGetProperty("question", out var questionValue)
            || questionValue.ValueKind != JsonValueKind.String)
        {
            return FollowUpDecision.None;
        }

        var question = questionValue.GetString()?.Trim();
        if (string.IsNullOrEmpty(question) || question.Length > MaxQuestionLength)
        {
            return FollowUpDecision.None;
        }

        return new FollowUpDecision(reason, question);
    }

    private static FollowUpReason ReadReason(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "missingpoint" or "missing_point" or "missing" => FollowUpReason.MissingPoint,
        "vague" or "unclear" => FollowUpReason.Vague,
        "contradictory" or "contradiction" => FollowUpReason.Contradictory,
        _ => FollowUpReason.Sufficient
    };

    /// <summary>The shape Ollama is told to produce, so the reply parses or not at all.</summary>
    private static object Schema => new
    {
        type = "object",
        properties = new
        {
            needsFollowUp = new { type = "boolean" },
            reason = new { type = "string", @enum = new[] { "Sufficient", "MissingPoint", "Vague", "Contradictory" } },
            question = new { type = "string" }
        },
        required = new[] { "needsFollowUp", "reason", "question" }
    };

    private static string BuildPrompt(FollowUpContext context)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Bạn là giám khảo vấn đáp. Hãy đọc câu trả lời của sinh viên và quyết định có hỏi thêm một câu không.");
        prompt.AppendLine();
        prompt.AppendLine("Quy tắc:");
        prompt.AppendLine("- Chỉ hỏi thêm khi câu trả lời THIẾU Ý so với yêu cầu, nói MƠ HỒ không đủ để chấm, hoặc MÂU THUẪN với chính nó.");
        prompt.AppendLine("- Nếu câu trả lời đã đủ để chấm, trả về reason = \"Sufficient\" và question = \"\".");
        prompt.AppendLine("- Câu hỏi thêm phải là MỘT câu hỏi ngắn, cụ thể, bằng tiếng Việt, không quá 300 ký tự.");
        prompt.AppendLine("- Không hỏi lại điều sinh viên đã trả lời rõ, và không lặp lại các câu đã hỏi ở dưới.");
        prompt.AppendLine("- Không tiết lộ đáp án mong đợi trong câu hỏi.");
        prompt.AppendLine();
        prompt.AppendLine("Câu hỏi gốc:");
        prompt.AppendLine(context.QuestionText);

        if (!string.IsNullOrWhiteSpace(context.ExpectedAnswer))
        {
            prompt.AppendLine();
            prompt.AppendLine("Đáp án mong đợi (chỉ để bạn đối chiếu, tuyệt đối không nhắc lại):");
            prompt.AppendLine(context.ExpectedAnswer);
        }

        if (context.Criteria.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Tiêu chí chấm:");
            foreach (var criterion in context.Criteria)
            {
                prompt.Append("- ").Append(criterion.Name);
                if (!string.IsNullOrWhiteSpace(criterion.Description))
                {
                    prompt.Append(": ").Append(criterion.Description);
                }

                prompt.AppendLine();
            }
        }

        if (context.PreviousProbes.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Các câu đã hỏi thêm cho câu này:");
            foreach (var probe in context.PreviousProbes)
            {
                prompt.Append("- ").AppendLine(probe);
            }
        }

        prompt.AppendLine();
        prompt.AppendLine("Câu trả lời của sinh viên (chuyển từ giọng nói, có thể sai chính tả):");
        prompt.AppendLine(context.Transcript);

        return prompt.ToString();
    }
}
