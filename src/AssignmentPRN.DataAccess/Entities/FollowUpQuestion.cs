namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// A question the AI examiner asked on top of a spoken answer, because the transcript
/// was missing a point, vague or self-contradictory. It hangs off the answer it probes,
/// so it disappears with that answer; <see cref="OrderNo"/> is 1 for the first probe of
/// that answer, 2 for the next, and so on.
/// </summary>
public class FollowUpQuestion
{
    public int FollowUpId { get; set; }

    public int AnswerId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    /// <summary>What the student said in reply. Null until they answer.</summary>
    public string? AnswerTranscript { get; set; }

    /// <summary>Key in the private audio store, never a path the browser can reach.</summary>
    public string? AudioPath { get; set; }

    /// <summary>Why the AI decided to probe: missing point, vague, or contradictory.</summary>
    public string? Reason { get; set; }

    /// <summary>1-based position among the probes of the same answer.</summary>
    public int OrderNo { get; set; } = 1;

    public DateTime? AskedAt { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public Answer Answer { get; set; } = null!;
}
