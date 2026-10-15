using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Presentation.ViewModels;

public class SessionResultViewModel
{
    public SessionResultResponse Session { get; init; } = new();
}

public class CandidateResultViewModel
{
    public CandidateResultResponse Candidate { get; init; } = new();

    /// <summary>Position of a main slot in the paper, so a follow-up can say which question it digs into.</summary>
    public int? OrderOf(int? examQuestionId) => Candidate.Paper.Questions
        .FirstOrDefault(question => question.ExamQuestionId == examQuestionId)?.OrderNo;

    /// <summary>What the AI suggested, or null when grading has not run for this slot.</summary>
    public CandidateReview? Review { get; set; }

    /// <summary>Why the AI side could not be read; the paper itself is still shown.</summary>
    public string? ReviewError { get; set; }
}

public static class ExamResultText
{
    /// <summary>Score out of 10 with one decimal, or a dash while the paper is not completed.</summary>
    public static string Score(decimal? score) => score?.ToString("0.0") ?? "—";

    public static string Ratio(int correct, int total) => total == 0 ? "—" : $"{correct}/{total}";
}
