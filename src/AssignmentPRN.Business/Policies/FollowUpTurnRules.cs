using AssignmentPRN.Business.Interfaces;

namespace AssignmentPRN.Business.Policies;

/// <summary>
/// When the AI examiner is allowed to ask one more thing, and how long the student then
/// has to answer it.
/// <para>
/// A probe is paid for out of the time left on the question it probes, which is what
/// <see cref="VivaBudgetRules.CanAskFollowUp"/> already assumes: a student who used
/// their whole allowance is not probed, a student who finished early can be. On top of
/// that, a probe is only worth asking if enough of that time is left for it to be read
/// aloud and answered — otherwise it would be cut off mid-sentence by the deadline.
/// </para>
/// <para>
/// Everything here takes <c>now</c> as an argument and touches no repository, so the
/// rules can be exercised without a database or a model.
/// </para>
/// </summary>
public static class FollowUpTurnRules
{
    /// <summary>
    /// Below this there is no point: text to speech has to read the question out and the
    /// student has to say something back.
    /// </summary>
    public static readonly TimeSpan MinimumTimeToProbe = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Whether a probe may be asked now.
    /// </summary>
    /// <param name="askedForQuestion">Probes already asked on this question.</param>
    /// <param name="askedInSlot">Probes already asked anywhere in this candidate's slot.</param>
    /// <param name="slotLimit">The session's <c>MaxFollowUpCount</c> for one candidate.</param>
    /// <param name="questionDeadline">When the question being probed runs out.</param>
    /// <param name="slotEnd">When the candidate's whole slot runs out.</param>
    public static bool CanAsk(
        int askedForQuestion,
        int askedInSlot,
        int slotLimit,
        ExamVivaSettings settings,
        DateTime now,
        DateTime questionDeadline,
        DateTime slotEnd)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!VivaBudgetRules.CanAskFollowUp(
                askedForQuestion, askedInSlot, slotLimit, settings, now, questionDeadline, slotEnd))
        {
            return false;
        }

        return Deadline(now, questionDeadline, slotEnd) - now >= MinimumTimeToProbe;
    }

    /// <summary>
    /// When an answer to the probe stops being accepted: whichever of the question's own
    /// deadline and the end of the slot comes first. A probe never buys extra time.
    /// </summary>
    public static DateTime Deadline(DateTime now, DateTime questionDeadline, DateTime slotEnd)
    {
        var deadline = questionDeadline < slotEnd ? questionDeadline : slotEnd;
        return deadline < now ? now : deadline;
    }

    /// <summary>
    /// How many probes are still allowed on this question, counting both budgets. Zero
    /// when either is used up; never negative.
    /// </summary>
    public static int Remaining(int askedForQuestion, int askedInSlot, int slotLimit, ExamVivaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var left = Math.Min(
            settings.MaxFollowUpsPerQuestion - askedForQuestion,
            slotLimit - askedInSlot);

        return left < 0 ? 0 : left;
    }
}
