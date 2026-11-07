using AssignmentPRN.Business.Interfaces;
namespace AssignmentPRN.Business.Policies;
public static class ExamSessionRules
{
    public static bool CanEdit(ExamSessionStatus status) => status is ExamSessionStatus.Draft or ExamSessionStatus.Scheduled;

    public static bool CanTransition(ExamSessionStatus from, ExamSessionStatus to) => (from, to) switch
    {
        (ExamSessionStatus.Draft, ExamSessionStatus.Scheduled or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.Scheduled, ExamSessionStatus.InProgress or ExamSessionStatus.Cancelled) => true,
        (ExamSessionStatus.InProgress, ExamSessionStatus.Completed or ExamSessionStatus.Cancelled) => true,
        _ => false
    };

    /// <summary>
    /// A session cannot be cancelled under a student who is still answering: their paper
    /// would be cut off halfway. They have to hand in, or be closed once their slot is over.
    /// </summary>
    public static bool BlocksCancellation(CandidateStatus status) => status == CandidateStatus.InProgress;

    /// <summary>
    /// What a slot becomes when its session is cancelled: a slot not sat yet is cancelled
    /// with it, while a handed-in paper or a recorded absence keeps its record.
    /// </summary>
    public static CandidateStatus StatusAfterCancellation(CandidateStatus status) =>
        status == CandidateStatus.Waiting ? CandidateStatus.Cancelled : status;

    /// <summary>A session whose slots a student may sit; a draft has not been published yet.</summary>
    public static bool CanSit(ExamSessionStatus status) =>
        status is ExamSessionStatus.Scheduled or ExamSessionStatus.InProgress;

    /// <summary>
    /// Whether <paramref name="now"/> falls inside the slot that starts at
    /// <paramref name="scheduledTime"/> and lasts <paramref name="minutes"/>.
    /// </summary>
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, int minutes) =>
        IsSlotOpen(now, scheduledTime, scheduledTime.AddMinutes(minutes));

    /// <summary>Same window, for callers that already hold the end of the slot.</summary>
    public static bool IsSlotOpen(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        now >= scheduledTime && now <= endTime;

    /// <summary>
    /// How long a paper is still accepted after the clock runs out. The countdown in the
    /// browser submits on its own at 00:00; this covers the seconds that request spends in
    /// flight, so work already done is not thrown away over a rounding difference.
    /// </summary>
    public static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Whether a paper may still be handed in. Wider than <see cref="IsSlotOpen"/>: a
    /// student cannot start late, but a submit that arrives moments late is still taken.
    /// </summary>
    public static bool CanSubmit(DateTime now, DateTime scheduledTime, DateTime endTime) =>
        now >= scheduledTime && now <= endTime + SubmitGrace;
}
