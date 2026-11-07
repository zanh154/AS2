using AssignmentPRN.Business.Services;
using Microsoft.AspNetCore.SignalR;

using AssignmentPRN.Presentation.Hubs;
namespace AssignmentPRN.Presentation.Realtime;

/// <summary>
/// Tells the examiner's screen how the AI marking of a slot is going. It goes to the
/// exam group, which only a lecturer who runs the session, its own students and admins
/// may join, and it carries no marks or transcripts: the page reloads the real figures
/// through the service, which checks ownership again.
/// <para>
/// Every send is best effort and fire and forget. Grading runs in a background service
/// with no request behind it, so a notification that fails has nobody to report to and
/// must never undo a mark that is already stored.
/// </para>
/// </summary>
public sealed class SignalRGradingNotifier(
    IHubContext<ExamUpdatesHub> hub,
    ILogger<SignalRGradingNotifier> logger) : IGradingNotifier
{
    public void Progress(GradingSlotInfo slot, int answersDone) => Send(slot.ExamId, new
    {
        examId = slot.ExamId,
        candidateId = slot.CandidateId,
        kind = "grading-progress",
        done = answersDone,
        total = slot.TotalAnswers
    });

    public void Finished(GradingSlotInfo slot, GradingOutcome outcome) => Send(slot.ExamId, new
    {
        examId = slot.ExamId,
        candidateId = slot.CandidateId,
        kind = "grading-done",
        graded = outcome.GradedAnswers,
        // The examiner needs to know a suggestion is partial before they trust it.
        skipped = outcome.SkippedAnswers,
        unmarkedCriteria = outcome.UnmarkedCriteria
    });

    private void Send(int examId, object payload) =>
        _ = Task.Run(async () =>
        {
            try { await hub.Clients.Group(ExamGroups.Exam(examId)).SendAsync("GradingChanged", payload); }
            catch (Exception ex) { logger.LogWarning(ex, "Grading notification failed for exam {ExamId}", examId); }
        });
}
