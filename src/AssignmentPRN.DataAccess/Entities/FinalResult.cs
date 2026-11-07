namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// The mark of one candidate in one exam. <see cref="AiScore"/> is what the grader
/// suggested, <see cref="LecturerScore"/> is what the examiner typed, and
/// <see cref="FinalScore"/> is the one that counts: the lecturer's when they entered
/// one, otherwise the AI's. Every change is also written to exam_logs, so an earlier
/// mark can still be traced after it is overwritten.
/// </summary>
public class FinalResult
{
    public int ResultId { get; set; }

    public int ExamId { get; set; }

    /// <summary>One row per candidate per exam.</summary>
    public int CandidateId { get; set; }

    public decimal? AiScore { get; set; }

    public decimal? LecturerScore { get; set; }

    public decimal? FinalScore { get; set; }

    public string? LecturerComment { get; set; }

    /// <summary>The lecturer or admin who last decided the mark.</summary>
    public int? FinalizedBy { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public ExamSession Session { get; set; } = null!;

    public ExamCandidate Candidate { get; set; } = null!;
}
