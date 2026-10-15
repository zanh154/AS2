using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>How a slot's grading ended, for the examiner and for the log.</summary>
/// <param name="GradedAnswers">Answers the model produced marks for.</param>
/// <param name="SkippedAnswers">Answers left for the examiner: no rubric, or no reply.</param>
/// <param name="UnmarkedCriteria">Criteria the model did not touch, across the slot.</param>
public sealed record GradingOutcome(int GradedAnswers, int SkippedAnswers, int UnmarkedCriteria, decimal? SlotScore);

public interface IGradingService
{
    /// <summary>
    /// Marks every answer of a finished slot and stores the suggestion. Safe to run
    /// again: it replaces its own previous suggestion and never overwrites a mark the
    /// lecturer decided.
    /// </summary>
    Task<ServiceResponse<GradingOutcome>> GradeCandidateAsync(int candidateId, CancellationToken ct = default);
}

/// <summary>
/// Turns what the model says about each answer into a stored suggestion. The business
/// rules live in <see cref="RubricScoring"/>, the model lives behind
/// <see cref="IAnswerGrader"/>, and this is the part that walks the slot and writes the
/// result — so an answer the model could not mark is reported rather than silently
/// scored zero.
/// </summary>
public sealed class GradingService(
    IGradingRepository repository,
    IAnswerGrader grader,
    TimeProvider clock,
    IGradingNotifier? notifier = null) : IGradingService
{
    public Task<ServiceResponse<GradingOutcome>> GradeCandidateAsync(int candidateId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var id = BusinessValidation.PositiveId(candidateId, "lượt thi");
            var slot = await repository.LoadAsync(id, ct)
                ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");

            // Marking a slot that is still being sat would score a paper half written.
            if (slot.Status != CandidateStatus.Completed)
                throw new BusinessValidationException("Chỉ chấm được lượt thi đã hoàn thành.");

            var evaluations = new List<AnswerEvaluation>();
            var weighted = new List<(bool IsFollowUp, decimal? Score)>();
            var unmarked = 0;
            var skipped = 0;
            var done = 0;

            foreach (var answer in slot.Answers)
            {
                var context = new GradingContext(
                    answer.QuestionText,
                    answer.ExpectedAnswer,
                    answer.Criteria.Select(x => new RubricCriterionInfo(x.CriteriaId, x.Name, x.Description, x.MaxScore)).ToArray(),
                    answer.Transcript,
                    answer.Probes.Select(x => new FollowUpExchange(x.QuestionText, x.AnswerTranscript)).ToArray());

                AnswerGrade grade;
                try
                {
                    grade = await grader.GradeAsync(context, ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // The contract says a grader swallows its own failures; one that
                    // breaks it leaves the answer for the examiner instead of the slot.
                    grade = AnswerGrade.None;
                }

                // Nothing came back: leave the answer unmarked so the examiner can see
                // it was never judged, rather than storing a zero.
                if (grade.Marks.Count == 0)
                {
                    skipped++;
                    weighted.Add((answer.IsFollowUpQuestion, null));
                    notifier?.Progress(slot, done + skipped);
                    continue;
                }

                var scored = RubricScoring.Apply(context.Criteria, grade.Marks);
                unmarked += scored.UnmarkedCount;
                weighted.Add((answer.IsFollowUpQuestion, scored.Score));

                evaluations.Add(new AnswerEvaluation(
                    answer.AnswerId,
                    scored.Score ?? 0m,
                    grade.Confidence,
                    Feedback(scored),
                    grade.Strengths,
                    grade.Weaknesses,
                    grade.MissingPoints,
                    scored.Criteria.Select(x => (x.CriteriaId, x.Awarded, x.Comment)).ToArray()));
                done++;
                notifier?.Progress(slot, done + skipped);
            }

            var slotScore = RubricScoring.Slot(weighted);
            if (evaluations.Count > 0 || slotScore is not null)
            {
                await repository.SaveAsync(slot.CandidateId, slot.ExamId, evaluations, slotScore,
                    clock.GetLocalNow().DateTime, ct);
            }

            var outcome = new GradingOutcome(done, skipped, unmarked, slotScore);
            notifier?.Finished(slot, outcome);
            return outcome;
        }, "Không thể chấm lượt thi này.");

    /// <summary>
    /// A one-line note on the answer as a whole, so the examiner sees at a glance when a
    /// suggestion is only partial.
    /// </summary>
    private static string? Feedback(ScoredAnswer scored) => scored.UnmarkedCount == 0
        ? null
        : $"AI chưa đánh giá {scored.UnmarkedCount} tiêu chí; phần đó đang tính 0 điểm, giảng viên cần xem lại.";
}

/// <summary>
/// Told how a slot's grading is going, so the examiner's screen can follow it. Every
/// call is best effort: a notification that fails must never undo a stored mark.
/// </summary>
public interface IGradingNotifier
{
    void Progress(GradingSlotRow slot, int answersDone);

    void Finished(GradingSlotRow slot, GradingOutcome outcome);
}
