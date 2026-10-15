using AssignmentPRN.DataAccess.Enums;
namespace AssignmentPRN.DataAccess.Contracts;
/// <summary>Decisions supplied by Business and evaluated within repository transactions.</summary>
public interface IExamStatePolicy
{
    bool CanEdit(ExamSessionStatus status);
    bool CanTransition(ExamSessionStatus from, ExamSessionStatus to);
    bool BlocksCancellation(CandidateStatus status);
    CandidateStatus StatusAfterCancellation(CandidateStatus status);
    bool CanSit(ExamSessionStatus status);
    bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes);
    ExamSessionStatus SessionAfterProgress(ExamSessionStatus current, IEnumerable<CandidateStatus> candidates);
    CandidateStatus? CloseOverdue(ExamSessionStatus session, CandidateStatus current, DateTime? scheduled, int minutes, DateTime now);
}
