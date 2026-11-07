namespace AssignmentPRN.Business.Policies;

/// <summary>
/// Who decides a mark, and what counts as a mark.
/// <para>
/// The AI only ever suggests. The moment a lecturer types a score, that score is the
/// one that counts and keeps counting: grading the slot again later must not quietly
/// take the decision back off them. Everything here is a plain function so the rules
/// can be read and tested without a database.
/// </para>
/// </summary>
public static class ResultAdjustmentRules
{
    public const decimal MinScore = 0m;

    public const decimal MaxScore = 10m;

    /// <summary>The mark that counts: the lecturer's if they entered one, else the AI's.</summary>
    public static decimal? FinalScore(decimal? lecturerScore, decimal? aiScore) => lecturerScore ?? aiScore;

    /// <summary>
    /// Whether a lecturer may enter this score. Null is allowed and means "take my score
    /// away again", handing the slot back to the AI suggestion.
    /// </summary>
    public static bool IsValidScore(decimal? score) =>
        score is null || (score >= MinScore && score <= MaxScore && decimal.Round(score.Value, 2) == score.Value);

    /// <summary>
    /// The line written to the exam log, so a mark that was overwritten can still be
    /// traced afterwards. Old and new are both named: the row itself only keeps the new
    /// one.
    /// </summary>
    public static string AuditLine(decimal? previous, decimal? next, string? comment)
    {
        var from = Describe(previous);
        var to = Describe(next);
        var line = $"Điểm chốt: {from} → {to}";
        return string.IsNullOrWhiteSpace(comment) ? line : $"{line}. Nhận xét: {Shorten(comment.Trim())}";
    }

    /// <summary>
    /// Invariant on purpose: this line is evidence in a grade dispute and has to read
    /// the same whatever culture the server that wrote it was running under.
    /// </summary>
    private static string Describe(decimal? score) =>
        score?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "chưa chốt";

    /// <summary>exam_logs.description holds 2000 characters; the line is cut to fit.</summary>
    private static string Shorten(string comment) => comment.Length <= 1500 ? comment : comment[..1500];
}
