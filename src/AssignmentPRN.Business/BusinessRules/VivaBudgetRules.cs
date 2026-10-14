using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.BusinessRules;
public static class VivaBudgetRules
{
    public static DateTime QuestionDeadline(DateTime startedAt, DateTime slotEnd, ExamVivaSettings settings) =>
        startedAt.AddSeconds(settings.SecondsPerQuestion) < slotEnd ? startedAt.AddSeconds(settings.SecondsPerQuestion) : slotEnd;
    public static bool CanAskFollowUp(int askedForQuestion, int askedInSession, int sessionLimit, ExamVivaSettings settings, DateTime now, DateTime questionDeadline, DateTime slotEnd) =>
        askedForQuestion >= 0 && askedInSession >= 0 && askedForQuestion < settings.MaxFollowUpsPerQuestion
        && askedInSession < sessionLimit && now < questionDeadline && now < slotEnd;
}
