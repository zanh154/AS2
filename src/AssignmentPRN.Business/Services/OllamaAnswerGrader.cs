using System.Text;
using System.Text.Json;
using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Marks one spoken answer against the rubric of its question, criterion by criterion,
/// and says what was strong, weak and missing. What comes back is a suggestion: nothing
/// here decides a grade, and <c>RubricScoring</c> is what makes the numbers safe to
/// store. Like the analyzer, a model that cannot be reached returns nothing rather than
/// failing the caller.
/// </summary>
public sealed class OllamaAnswerGrader(OllamaJsonClient client) : IAnswerGrader
{
    /// <summary>
    /// Grading runs after the slot, not between two questions, so it can afford to wait
    /// longer than the probe analyzer does.
    /// </summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromMinutes(2);

    /// <summary>Longer than this and the model is writing an essay, not a note.</summary>
    internal const int MaxNoteLength = 2000;

    public async Task<AnswerGrade> GradeAsync(GradingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // With no rubric there is nothing to mark against, and with nothing said there
        // is nothing to mark. Either way the examiner marks this one by hand.
        if (context.Criteria.Count == 0) return AnswerGrade.None;
        if (string.IsNullOrWhiteSpace(context.Transcript)
            && context.FollowUps.All(x => string.IsNullOrWhiteSpace(x.Transcript)))
        {
            return AnswerGrade.None;
        }

        using var answer = await client.GenerateAsync(
            BuildPrompt(context), Schema(context), Budget, maxTokens: 1200, cancellationToken);

        return answer is null ? AnswerGrade.None : Parse(answer);
    }

    /// <summary>
    /// Reads the model's JSON. Anything unusable is left out rather than guessed at:
    /// a criterion with no number is simply not marked, and <c>RubricScoring</c> then
    /// reports it as unmarked to the examiner.
    /// </summary>
    internal static AnswerGrade Parse(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return AnswerGrade.None;

        var marks = new List<CriterionMark>();
        if (root.TryGetProperty("criteria", out var criteria) && criteria.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in criteria.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("criteriaId", out var id) || !id.TryGetInt32(out var criteriaId)) continue;
                if (!item.TryGetProperty("score", out var score) || score.ValueKind != JsonValueKind.Number) continue;
                if (!score.TryGetDecimal(out var value) || !IsFinite(value)) continue;

                marks.Add(new CriterionMark(criteriaId, value, ReadNote(item, "comment")));
            }
        }

        var confidence = root.TryGetProperty("confidence", out var stated)
            && stated.ValueKind == JsonValueKind.Number
            && stated.TryGetDecimal(out var parsed)
            && parsed is >= 0m and <= 1m
            ? parsed
            : (decimal?)null;

        return new AnswerGrade(marks, ReadNote(root, "strengths"), ReadNote(root, "weaknesses"),
            ReadNote(root, "missingPoints"), confidence);
    }

    private static bool IsFinite(decimal value) => value is > -1_000_000m and < 1_000_000m;

    private static string? ReadNote(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= MaxNoteLength ? text : text[..MaxNoteLength];
    }

    /// <summary>
    /// Pins the reply to this rubric: the model is told which ids exist, so a mark for
    /// something that is not being graded is less likely in the first place.
    /// </summary>
    private static object Schema(GradingContext context) => new
    {
        type = "object",
        properties = new
        {
            criteria = new
            {
                type = "array",
                minItems = context.Criteria.Count,
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        criteriaId = new { type = "integer", @enum = context.Criteria.Select(x => x.CriteriaId).ToArray() },
                        score = new { type = "number" },
                        comment = new { type = "string" }
                    },
                    required = new[] { "criteriaId", "score", "comment" }
                }
            },
            strengths = new { type = "string" },
            weaknesses = new { type = "string" },
            missingPoints = new { type = "string" },
            confidence = new { type = "number" }
        },
        required = new[] { "criteria", "strengths", "weaknesses", "missingPoints", "confidence" }
    };

    private static string BuildPrompt(GradingContext context)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Bạn là giám khảo vấn đáp. Hãy chấm câu trả lời của sinh viên theo từng tiêu chí trong rubric.");
        prompt.AppendLine();
        prompt.AppendLine("Quy tắc:");
        prompt.AppendLine("- Chấm TỪNG tiêu chí, cho điểm trong khoảng từ 0 đến điểm tối đa của tiêu chí đó.");
        prompt.AppendLine("- Dùng đúng criteriaId được liệt kê, không tự tạo tiêu chí mới, không bỏ sót tiêu chí nào.");
        prompt.AppendLine("- Nhận xét ngắn gọn bằng tiếng Việt, nêu căn cứ lấy từ lời của sinh viên.");
        prompt.AppendLine("- Đây là bản ghi từ giọng nói: bỏ qua lỗi chính tả, chỉ chấm nội dung.");
        prompt.AppendLine("- confidence là mức độ chắc chắn của bạn, từ 0 đến 1.");
        prompt.AppendLine();
        prompt.AppendLine("Câu hỏi:");
        prompt.AppendLine(context.QuestionText);

        if (!string.IsNullOrWhiteSpace(context.ExpectedAnswer))
        {
            prompt.AppendLine();
            prompt.AppendLine("Đáp án mong đợi:");
            prompt.AppendLine(context.ExpectedAnswer);
        }

        prompt.AppendLine();
        prompt.AppendLine("Rubric:");
        foreach (var criterion in context.Criteria)
        {
            prompt.Append("- criteriaId ").Append(criterion.CriteriaId).Append(" · ").Append(criterion.Name)
                .Append(" · điểm tối đa ").Append(criterion.MaxScore);
            if (!string.IsNullOrWhiteSpace(criterion.Description)) prompt.Append(": ").Append(criterion.Description);
            prompt.AppendLine();
        }

        prompt.AppendLine();
        prompt.AppendLine("Câu trả lời của sinh viên:");
        prompt.AppendLine(string.IsNullOrWhiteSpace(context.Transcript) ? "(không nói gì)" : context.Transcript);

        if (context.FollowUps.Count > 0)
        {
            prompt.AppendLine();
            prompt.AppendLine("Phần hỏi thêm (tính chung vào câu trả lời này):");
            foreach (var exchange in context.FollowUps)
            {
                prompt.Append("- Hỏi: ").AppendLine(exchange.Question);
                prompt.Append("  Đáp: ").AppendLine(string.IsNullOrWhiteSpace(exchange.Transcript) ? "(không nói gì)" : exchange.Transcript);
            }
        }

        return prompt.ToString();
    }
}
