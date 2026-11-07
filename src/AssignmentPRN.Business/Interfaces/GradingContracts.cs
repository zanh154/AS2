namespace AssignmentPRN.Business.Interfaces;

/// <summary>Why the AI examiner decided to probe a spoken answer further.</summary>
public enum FollowUpReason
{
    /// <summary>The answer covered the question; nothing to ask.</summary>
    Sufficient = 0,

    /// <summary>A point the question asks for is absent from the transcript.</summary>
    MissingPoint = 1,

    /// <summary>The point is touched on but too loosely to judge.</summary>
    Vague = 2,

    /// <summary>The transcript says two things that cannot both hold.</summary>
    Contradictory = 3
}

/// <summary>One criterion of a rubric, as the AI prompts need to see it.</summary>
public sealed record RubricCriterionInfo(int CriteriaId, string Name, string? Description, decimal MaxScore);

/// <summary>
/// Everything the analyzer is allowed to read about one answer. Built in the business
/// layer so the analyzer never touches a repository, and so a test can hand it a plain
/// object.
/// </summary>
public sealed record FollowUpContext(
    string QuestionText,
    string? ExpectedAnswer,
    IReadOnlyList<RubricCriterionInfo> Criteria,
    string? Transcript,
    IReadOnlyList<string> PreviousProbes);

/// <summary>
/// What the analyzer suggests. <see cref="Question"/> is set only when
/// <see cref="Reason"/> is not <see cref="FollowUpReason.Sufficient"/>.
/// </summary>
public sealed record FollowUpDecision(FollowUpReason Reason, string? Question)
{
    /// <summary>The answer stands on its own, or the AI could not be reached.</summary>
    public static readonly FollowUpDecision None = new(FollowUpReason.Sufficient, null);

    public bool NeedsFollowUp => Reason != FollowUpReason.Sufficient && !string.IsNullOrWhiteSpace(Question);
}

/// <summary>
/// Reads a transcript against its question and rubric and decides whether to probe.
/// An implementation must never throw: a model that is unreachable, slow or answering
/// nonsense returns <see cref="FollowUpDecision.None"/>, so the exam carries on without
/// a probe rather than failing.
/// </summary>
public interface IFollowUpAnalyzer
{
    Task<FollowUpDecision> AnalyzeAsync(FollowUpContext context, CancellationToken cancellationToken = default);
}

/// <summary>The mark the grader suggests for one rubric criterion.</summary>
public sealed record CriterionMark(int CriteriaId, decimal Score, string? Comment);

/// <summary>
/// What the grader suggests for one answer, before any clamping. Scores here are raw
/// model output; <c>RubricScoring</c> is what makes them safe to store.
/// </summary>
public sealed record AnswerGrade(
    IReadOnlyList<CriterionMark> Marks,
    string? Strengths,
    string? Weaknesses,
    string? MissingPoints,
    decimal? Confidence)
{
    /// <summary>Nothing was graded: the model was unreachable or unusable.</summary>
    public static readonly AnswerGrade None = new([], null, null, null, null);
}

/// <summary>Everything the grader may read about one answer.</summary>
public sealed record GradingContext(
    string QuestionText,
    string? ExpectedAnswer,
    IReadOnlyList<RubricCriterionInfo> Criteria,
    string? Transcript,
    IReadOnlyList<FollowUpExchange> FollowUps);

/// <summary>A probe and what the student said back to it.</summary>
public sealed record FollowUpExchange(string Question, string? Transcript);

/// <summary>
/// Marks one answer against its rubric. Like <see cref="IFollowUpAnalyzer"/> it must
/// never throw: an unusable model returns <see cref="AnswerGrade.None"/> and the answer
/// stays unmarked for the lecturer to handle.
/// </summary>
public interface IAnswerGrader
{
    Task<AnswerGrade> GradeAsync(GradingContext context, CancellationToken cancellationToken = default);
}
