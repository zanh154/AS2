using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.Policies;

/// <summary>What a criterion ended up worth, after the raw model output was made safe.</summary>
/// <param name="Awarded">Clamped into the criterion's own range.</param>
/// <param name="Marked">False when the model said nothing about this criterion.</param>
public sealed record ScoredCriterion(int CriteriaId, decimal Awarded, decimal MaxScore, string? Comment, bool Marked);

/// <summary>
/// One answer's suggested mark: every criterion of the rubric, and the ten-point score
/// they add up to. <see cref="Score"/> is null when there was nothing to mark against.
/// </summary>
public sealed record ScoredAnswer(IReadOnlyList<ScoredCriterion> Criteria, decimal? Score)
{
    /// <summary>Criteria the model skipped. The examiner is shown these explicitly.</summary>
    public int UnmarkedCount => Criteria.Count(x => !x.Marked);
}

/// <summary>
/// Turns what the model said into marks that can be stored.
/// <para>
/// Raw model output is not trusted with a student's grade: a score above the criterion's
/// maximum, a negative one, a criterion invented out of nothing, or a criterion quietly
/// skipped are all things a language model does, and each one has a defined outcome here
/// rather than reaching the database. Nothing in this file talks to a repository or a
/// clock, so every rule can be exercised on its own.
/// </para>
/// </summary>
public static class RubricScoring
{
    public const decimal MaxScore = 10m;

    /// <summary>Shown where a criterion has no mark, so a zero is never silent.</summary>
    public const string NotMarkedComment = "AI không đánh giá tiêu chí này.";

    /// <summary>
    /// Marks one answer. Every criterion of <paramref name="rubric"/> comes back, in the
    /// order given: a criterion the model skipped scores zero and says so, and a mark
    /// for a criterion that is not in the rubric is dropped.
    /// </summary>
    public static ScoredAnswer Apply(IReadOnlyList<RubricCriterionInfo> rubric, IReadOnlyList<CriterionMark> marks)
    {
        ArgumentNullException.ThrowIfNull(rubric);
        ArgumentNullException.ThrowIfNull(marks);

        // A model that repeats a criterion gets its first word on it, not its last.
        var byCriteria = new Dictionary<int, CriterionMark>();
        foreach (var mark in marks) byCriteria.TryAdd(mark.CriteriaId, mark);

        var scored = new List<ScoredCriterion>(rubric.Count);
        foreach (var criterion in rubric)
        {
            var max = criterion.MaxScore < 0 ? 0 : criterion.MaxScore;
            if (!byCriteria.TryGetValue(criterion.CriteriaId, out var mark))
            {
                scored.Add(new ScoredCriterion(criterion.CriteriaId, 0m, max, NotMarkedComment, Marked: false));
                continue;
            }

            scored.Add(new ScoredCriterion(criterion.CriteriaId, Clamp(mark.Score, max), max, Trim(mark.Comment), Marked: true));
        }

        return new ScoredAnswer(scored, Total(scored));
    }

    /// <summary>
    /// The ten-point score of a marked answer, or null when the rubric is worth nothing
    /// at all: scoring against it would read as a zero the student earned.
    /// </summary>
    public static decimal? Total(IReadOnlyList<ScoredCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        decimal max = 0, awarded = 0;
        foreach (var criterion in criteria)
        {
            max += criterion.MaxScore;
            awarded += criterion.Awarded;
        }

        return max <= 0 ? null : Round(awarded * MaxScore / max);
    }

    /// <summary>
    /// The mark for a whole slot: the answers, weighted the way the question bank
    /// already weights them — a probe counts half of the question it probes. Answers
    /// with no score of their own are left out rather than counted as zero, so a rubric
    /// nobody wrote does not drag the slot down. Null when nothing could be marked.
    /// </summary>
    public static decimal? Slot(IEnumerable<(bool IsFollowUp, decimal? Score)> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        decimal weightTotal = 0, earned = 0;
        foreach (var (isFollowUp, score) in answers)
        {
            if (score is not decimal value) continue;
            var weight = isFollowUp ? 0.5m : 1m;
            weightTotal += weight;
            earned += weight * value;
        }

        return weightTotal == 0 ? null : Round(earned / weightTotal);
    }

    private static decimal Clamp(decimal score, decimal max) =>
        score < 0 ? 0 : score > max ? max : score;

    private static string? Trim(string? comment)
    {
        var trimmed = comment?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        // The column holds 1000 characters; a model that rambles is cut, not refused.
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }

    /// <summary>One decimal, half away from zero, matching the question bank's scores.</summary>
    private static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
