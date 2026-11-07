using AssignmentPRN.DataAccess.Contracts;
using Data = AssignmentPRN.DataAccess.Enums;
using Biz = AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.Policies;
/// <summary>Maps persistence state to business rules without accessing the database.</summary>
public sealed class ExamStatePolicy : IExamStatePolicy
{
    public bool CanEdit(Data.ExamSessionStatus status) => ExamSessionRules.CanEdit((Biz.ExamSessionStatus)status);
    public bool CanTransition(Data.ExamSessionStatus from, Data.ExamSessionStatus to) => ExamSessionRules.CanTransition((Biz.ExamSessionStatus)from, (Biz.ExamSessionStatus)to);
    public bool BlocksCancellation(Data.CandidateStatus status) => ExamSessionRules.BlocksCancellation((Biz.CandidateStatus)status);
    public Data.CandidateStatus StatusAfterCancellation(Data.CandidateStatus status) => (Data.CandidateStatus)ExamSessionRules.StatusAfterCancellation((Biz.CandidateStatus)status);
    public bool CanSit(Data.ExamSessionStatus status) => ExamSessionRules.CanSit((Biz.ExamSessionStatus)status);
    public bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) => ExamSessionRules.IsSlotOpen(now, scheduledTime, minutes);
    public Data.ExamSessionStatus SessionAfterProgress(Data.ExamSessionStatus current, IEnumerable<Data.CandidateStatus> candidates) => (Data.ExamSessionStatus)ExamLifecycleRules.SessionAfterProgress((Biz.ExamSessionStatus)current, candidates.Select(x => (Biz.CandidateStatus)x));
    public Data.CandidateStatus? CloseOverdue(Data.ExamSessionStatus session, Data.CandidateStatus current, DateTime? scheduled, int minutes, DateTime now) => (Data.CandidateStatus?)ExamLifecycleRules.CloseOverdue((Biz.ExamSessionStatus)session, (Biz.CandidateStatus)current, scheduled, minutes, now);
}
