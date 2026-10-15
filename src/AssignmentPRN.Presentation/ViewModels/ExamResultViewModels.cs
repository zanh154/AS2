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
}

public static class ExamResultText
{
    /// <summary>Score out of 10 with one decimal, or a dash while the paper is not completed.</summary>
    public static string Score(decimal? score) => score?.ToString("0.0") ?? "—";

    public static string Ratio(int correct, int total) => total == 0 ? "—" : $"{correct}/{total}";
}
