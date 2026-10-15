using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Services;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;

namespace AssignmentPRN.Business.Services;

public sealed class VoiceRoomService(IVoiceRoomRepository repository, IQuestionService questions,
    TimeProvider clock, IVoiceAudioStore? audioStore = null, IVoiceRoomNotifier? notifier = null,
    IFollowUpAnalyzer? analyzer = null, GradingQueue? grading = null) : IVoiceRoomService
{
    /// <summary>
    /// How long the room holds the next question closed while the examiner decides
    /// whether to probe. Longer than the analyzer's own budget so a decision that is
    /// merely slow still lands, short enough that a crashed call does not strand the
    /// student. Past it the next question opens as if no probe had been considered.
    /// </summary>
    internal static readonly TimeSpan AnalysisGrace = TimeSpan.FromSeconds(25);

    public Task<bool> IsVoiceAsync(int candidateId, int studentId, CancellationToken ct = default) =>
        repository.IsVoiceAsync(candidateId, studentId, ct);

    public Task<ServiceResponse<bool>> CanSubscribeAsync(int candidateId, int studentId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(() => repository.UpdateAsync(candidateId, studentId, a => {
            ValidateOwner(a);
            if (a.Logs.Count == 0) throw new BusinessValidationException("Lượt thi chưa mở chế độ giọng nói.");
            return true;
        }, ct), "Không thể đăng ký phòng thi.");

    public Task<ServiceResponse<VoiceRoomState>> UploadAsync(int candidateId, int studentId, int questionId,
        string contentType, long length, double durationSeconds, Stream content, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => {
            if (length <= 0 || length > LocalVoiceAudioStore.MaxBytes || !double.IsFinite(durationSeconds) || durationSeconds <= 0)
                throw new BusinessValidationException("Bản thu phải có dữ liệu, không quá 12 MB và có thời lượng hợp lệ.");
            await repository.UpdateAsync(candidateId, studentId, a => {
                Apply(a, null);
                ValidateUpload(a, questionId, durationSeconds);
                return true;
            }, ct);
            var key = await (audioStore ?? throw new InvalidOperationException("Chưa cấu hình kho âm thanh."))
                .SaveAsync(candidateId, questionId, contentType, content, ct);
            return await repository.UpdateAsync(candidateId, studentId, a => {
                Apply(a, null);
                ValidateUpload(a, questionId, durationSeconds);
                var q = a.Questions.Single(x => x.ExamQuestionId == questionId);
                var answer = AnswerFor(a, q, Now);
                // A probe is the open item, so the recording belongs to the probe rather
                // than to the answer underneath it.
                var probe = OpenProbe(a);
                if (probe != null)
                {
                    if (probe.AudioPath != null && probe.AudioPath != key)
                        throw new BusinessValidationException("Không thể thay bản thu của câu đã nộp.");
                    probe.AudioPath = key;
                    return Apply(a, null);
                }
                if (q.IsCompleted && answer.AudioPath != null && answer.AudioPath != key)
                    throw new BusinessValidationException("Không thể thay bản thu của câu đã nộp.");
                answer.AudioPath = key;
                return Apply(a, null);
            }, ct);
        }, "Không thể lưu âm thanh. Giữ bản thu và thử lại.");

    public Task<ServiceResponse<VoiceAudioDownload>> DownloadAsync(int candidateId, int studentId, int questionId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => {
            var key = await repository.UpdateAsync(candidateId, studentId, a => {
                ValidateOwner(a);
                if (a.Logs.Count == 0 || !a.Questions.Any(x => x.ExamQuestionId == questionId))
                    throw new BusinessValidationException("Không có quyền truy cập bản thu.");
                return a.Answers.SingleOrDefault(x => x.ExamQuestionId == questionId)?.AudioPath
                    ?? throw new BusinessValidationException("Câu trả lời chưa có bản thu.");
            }, ct);
            var file = await (audioStore ?? throw new InvalidOperationException()).OpenAsync(key, ct);
            return new VoiceAudioDownload(file.Content, file.ContentType);
        }, "Không thể tải bản thu.");

    private void ValidateUpload(VoiceRoomAggregate a, int questionId, double duration)
    {
        ValidateOwner(a);
        var target = a.Questions.SingleOrDefault(x => x.ExamQuestionId == questionId);
        if (a.Logs.Count == 0 || target?.AskedAt == null || duration > a.Settings.SecondsPerQuestion
            || duration > Math.Min((Now - target.AskedAt.Value).TotalSeconds + 2,
                (Deadline(a, target) - target.AskedAt.Value).TotalSeconds)
            || a.Candidate.Status is CandidateStatus.Cancelled or CandidateStatus.Absent)
            throw new BusinessValidationException("Câu hỏi hoặc thời lượng bản thu không còn hợp lệ.");
        var existing = a.Answers.SingleOrDefault(x => x.ExamQuestionId == questionId);

        // While a probe on this question is open the question itself reads as completed,
        // but the slot is still live and the recording is the probe's.
        var probe = OpenProbe(a);
        if (probe != null)
        {
            if (ParentOf(a, probe)?.ExamQuestionId != questionId)
                throw new BusinessValidationException("Câu hỏi không còn nhận bản thu.");
            ValidateWindow(a, Now);
            if (Now >= ProbeDeadline(a, probe))
                throw new BusinessValidationException("Câu hỏi không còn nhận bản thu.");
            return;
        }

        // Retry of an immutable file, or bounded transfer grace for a recording stopped by
        // timeout. This never extends the question deadline or accepts late transcript edits.
        if (target.IsCompleted && (existing?.AudioPath != null
            || existing?.FinishedAt == Deadline(a, target) && Now <= Deadline(a, target) + ExamSessionRules.SubmitGrace)) return;
        ValidateWindow(a, Now);
        if (target != a.Questions.FirstOrDefault(x => !x.IsCompleted) || Now >= Deadline(a, target))
            throw new BusinessValidationException("Câu hỏi không còn nhận bản thu.");
    }

    public async Task<ServiceResponse<VoiceRoomState>> EnterAsync(int candidateId, int studentId, CancellationToken ct = default)
    {
        if (!await repository.IsVoiceAsync(candidateId, studentId, ct))
        {
            var entered = await questions.EnterExamAsync(candidateId, studentId, ct);
            if (!entered.Success) return ServiceResponse<VoiceRoomState>.Fail(entered.Error ?? "Không thể vào thi.");
        }
        return await ServiceExecutor.RunAsync(() => repository.UpdateAsync(candidateId, studentId, aggregate => {
            ValidateOwner(aggregate);
            if (aggregate.Logs.Count == 0)
            {
                ValidateWindow(aggregate, Now);
                if (aggregate.Answers.Any(x => x.SelectedOptionId != null || x.FinishedAt != null)
                    || aggregate.Questions.Any(x => x.IsCompleted || x.AskedAt != null))
                    throw new BusinessValidationException("Lượt thi đã bắt đầu ở chế độ khác; hãy tiếp tục phòng thi hiện tại.");
                aggregate.Logs.Add(new ExamLog { ExamId = aggregate.Candidate.ExamId, CandidateId = candidateId,
                    UserId = studentId, Action = VoiceRoomRepository.StartedAction, Timestamp = Now });
            }
            return Apply(aggregate, null);
        }, ct), "Không thể vào phòng thi giọng nói.");
    }

    public async Task<ServiceResponse<VoiceRoomState>> SyncAsync(int candidateId, int studentId,
        VoiceAnswerRequest? answer = null, CancellationToken ct = default)
    {
        var changed = false;
        var finished = false;
        var result = await ServiceExecutor.RunAsync(() => repository.UpdateAsync(candidateId, studentId, a => {
            var before = (a.Candidate.Status, Completed: a.Questions.Count(x => x.IsCompleted));
            var state = Apply(a, answer);
            changed = before != (a.Candidate.Status, a.Questions.Count(x => x.IsCompleted));
            finished = before.Status != CandidateStatus.Completed
                && a.Candidate.Status == CandidateStatus.Completed;
            return state;
        }, ct), "Không thể đồng bộ phòng thi. Hãy thử lại.");

        // The examiner only thinks between turns, never inside the transaction above:
        // a model call can take seconds and would hold the candidate row locked.
        if (result.Success && result.Data?.AwaitingFollowUp == true)
        {
            // Either a probe is asked, or the turn is released so the next question can
            // open. Leaving the room waiting would cost the student the grace window for
            // a decision that has already been made.
            var next = await TryAskFollowUpAsync(candidateId, studentId, ct)
                ?? await ReleaseHoldAsync(candidateId, studentId, ct);
            if (next is not null)
            {
                result = ServiceResponse<VoiceRoomState>.Ok(next);
                changed = true;
            }
        }

        // Marking takes a model call per answer, far longer than a request may stay
        // open, so the finished slot is queued rather than graded here.
        if (result.Success && finished && grading != null)
        {
            try { await grading.QueueCandidateAsync(candidateId, ct); }
            catch (Exception) { /* The background service also sweeps up ungraded slots. */ }
        }

        if (result.Success && changed && notifier != null)
        {
            // The transaction is committed before a best-effort invalidation is sent.
            // Polling/reconnect still works when delivery fails.
            try { await notifier.ChangedAsync(candidateId, studentId, ct); }
            catch (Exception) { /* A notification failure cannot undo an accepted answer. */ }
        }
        return result;
    }

    /// <summary>
    /// Asks the analyzer whether the answer just handed in needs probing and, if so,
    /// writes the probe. Returns the new room state, or null when nothing was asked —
    /// including every failure, because a viva must not stop for a model.
    /// </summary>
    private async Task<VoiceRoomState?> TryAskFollowUpAsync(int candidateId, int studentId, CancellationToken ct)
    {
        if (analyzer is null) return null;

        FollowUpContext? context;
        try
        {
            context = await repository.UpdateAsync(candidateId, studentId, BuildFollowUpContext, ct);
        }
        catch (Exception) { return null; }
        if (context is null) return null;

        FollowUpDecision decision;
        try
        {
            decision = await analyzer.AnalyzeAsync(context, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // The contract says an analyzer swallows its own failures, but a viva is on
            // the line: one that breaks the contract still must not break the exam.
            return null;
        }
        if (!decision.NeedsFollowUp) return null;

        try
        {
            var asked = await repository.UpdateAsync(candidateId, studentId, a => {
                // The budget is checked again here: time has passed since the decision
                // was asked for, and another request may have moved the room on.
                var target = ProbeTarget(a, Now);
                if (target is null) return false;
                var answer = target.Value.Answer;
                // This transaction follows the one that wrote the answer, so the id is
                // real. If it is not, something moved underneath us: ask nothing.
                if (answer.AnswerId <= 0) return false;
                a.FollowUps.Add(new FollowUpQuestion
                {
                    AnswerId = answer.AnswerId,
                    QuestionText = decision.Question!,
                    Reason = decision.Reason.ToString(),
                    OrderNo = ProbesFor(a, answer.AnswerId).Count + 1,
                    AskedAt = Now
                });
                return true;
            }, ct);

            // Read the room back in its own transaction: the probe only has an id once
            // the insert above is saved, and the client answers it by that id.
            return asked
                ? await repository.UpdateAsync(candidateId, studentId, a => Apply(a, null), ct)
                : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Opens the next question after the examiner decided not to probe, or failed to
    /// decide at all. Null when the write could not be made, in which case the hold
    /// expires on its own after <see cref="AnalysisGrace"/>.
    /// </summary>
    private async Task<VoiceRoomState?> ReleaseHoldAsync(int candidateId, int studentId, CancellationToken ct)
    {
        try
        {
            return await repository.UpdateAsync(candidateId, studentId, a => Apply(a, null, releaseHold: true), ct);
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// What the analyzer may read, or null when no probe is due. Mutates nothing.
    /// </summary>
    private FollowUpContext? BuildFollowUpContext(VoiceRoomAggregate a)
    {
        var target = ProbeTarget(a, Now);
        if (target is null) return null;
        var (question, answer) = target.Value;
        var probes = ProbesFor(a, answer.AnswerId);

        return new FollowUpContext(
            question.Question.QuestionText,
            question.Question.ExpectedAnswer,
            a.CriteriaByQuestion.TryGetValue(question.QuestionId, out var criteria)
                ? criteria.Select(x => new RubricCriterionInfo(x.CriteriaId, x.CriteriaName, x.Description, x.MaxScore)).ToArray()
                : [],
            // The last thing the student said: the answer itself, or their reply to the
            // probe before this one.
            probes.LastOrDefault()?.AnswerTranscript ?? answer.Transcript,
            probes.Select(x => x.QuestionText).ToArray());
    }

    /// <summary>
    /// The question a probe would be asked about, with its answer, or null when probing
    /// is not allowed right now. This is the single place the budget, the clock and the
    /// grace window are decided, so the state, the analyzer call and the write cannot
    /// disagree.
    /// </summary>
    private (ExamQuestion Question, Answer Answer)? ProbeTarget(VoiceRoomAggregate a, DateTime now)
    {
        if (a.Candidate.Status != CandidateStatus.InProgress || a.Logs.Count == 0) return null;
        if (OpenProbe(a) != null) return null;

        // The turn that just ended: the completed question whose answer was finished last.
        var candidates = a.Questions
            .Where(x => x.IsCompleted)
            .Select(question => (Question: question, Answer: a.Answers.SingleOrDefault(x => x.ExamQuestionId == question.ExamQuestionId)))
            // The answer may still be unsaved: the turn that just ended is decided in the
            // same transaction that writes it. Only the write path needs a real id.
            .Where(x => x.Answer is { FinishedAt: not null })
            .ToList();
        if (candidates.Count == 0) return null;

        var last = candidates.MaxBy(x => x.Answer!.FinishedAt!.Value);
        var answer = last.Answer!;
        var probes = ProbesFor(a, answer.AnswerId);

        // A probe is considered right after the turn ends, not minutes later.
        var turnEndedAt = probes.LastOrDefault()?.AnsweredAt ?? answer.FinishedAt!.Value;
        if (now >= turnEndedAt + AnalysisGrace) return null;

        var settings = new Interfaces.ExamVivaSettings(a.Settings.SecondsPerQuestion, a.Settings.MaxFollowUpsPerQuestion);
        return FollowUpTurnRules.CanAsk(probes.Count, a.FollowUps.Count, a.Candidate.Session.MaxFollowUpCount,
            settings, now, Deadline(a, last.Question), SlotEnd(a))
            ? (last.Question, answer)
            : null;
    }

    private DateTime Now => clock.GetLocalNow().DateTime;

    private static void ValidateOwner(VoiceRoomAggregate a)
    {
        if (a.Candidate.Student.Role.RoleName != "Student" || a.Candidate.Student.Status != "Active")
            throw new BusinessValidationException("Tài khoản sinh viên không còn hoạt động.");
    }

    private static DateTime SlotEnd(VoiceRoomAggregate a)
    {
        var scheduled = a.Candidate.ScheduledTime ?? throw new BusinessValidationException("Lượt thi chưa được xếp giờ.");
        return new[] { scheduled.AddMinutes(a.Candidate.Session.TimePerStudent), a.Candidate.Session.EndTime,
            (a.Candidate.StartedAt ?? scheduled).AddMinutes(a.Candidate.Session.TimePerStudent) }.Min();
    }

    private static void ValidateWindow(VoiceRoomAggregate a, DateTime now)
    {
        if (!ExamSessionRules.CanSit((Interfaces.ExamSessionStatus)a.Candidate.Session.Status)
            || a.Candidate.Status != CandidateStatus.InProgress || a.Candidate.StartedAt == null
            || a.Candidate.ScheduledTime == null || now < a.Candidate.ScheduledTime || now >= SlotEnd(a))
            throw new BusinessValidationException("Phiên thi hoặc lượt thi không còn cho phép trả lời.");
    }

    private DateTime Deadline(VoiceRoomAggregate a, ExamQuestion q) => VivaBudgetRules.QuestionDeadline(
        q.AskedAt!.Value, SlotEnd(a), new Interfaces.ExamVivaSettings(a.Settings.SecondsPerQuestion, a.Settings.MaxFollowUpsPerQuestion));

    private static List<FollowUpQuestion> ProbesFor(VoiceRoomAggregate a, int answerId) =>
        a.FollowUps.Where(x => x.AnswerId == answerId).OrderBy(x => x.OrderNo).ThenBy(x => x.FollowUpId).ToList();

    private static FollowUpQuestion? OpenProbe(VoiceRoomAggregate a) =>
        a.FollowUps.Where(x => x.AskedAt != null && x.AnsweredAt == null)
            .OrderBy(x => x.OrderNo).ThenBy(x => x.FollowUpId).FirstOrDefault();

    private static ExamQuestion? ParentOf(VoiceRoomAggregate a, FollowUpQuestion probe)
    {
        var answer = a.Answers.SingleOrDefault(x => x.AnswerId == probe.AnswerId);
        return answer is null ? null : a.Questions.SingleOrDefault(x => x.ExamQuestionId == answer.ExamQuestionId);
    }

    /// <summary>A probe never buys time past the question it probes or past the slot.</summary>
    private DateTime ProbeDeadline(VoiceRoomAggregate a, FollowUpQuestion probe)
    {
        var parent = ParentOf(a, probe);
        var questionDeadline = parent is null ? SlotEnd(a) : Deadline(a, parent);
        return FollowUpTurnRules.Deadline(probe.AskedAt!.Value, questionDeadline, SlotEnd(a));
    }

    private static Answer AnswerFor(VoiceRoomAggregate a, ExamQuestion q, DateTime now)
    {
        var answer = a.Answers.SingleOrDefault(x => x.ExamQuestionId == q.ExamQuestionId);
        if (answer != null) return answer;
        answer = new Answer { CandidateId = a.Candidate.CandidateId, ExamQuestionId = q.ExamQuestionId,
            StartedAt = q.AskedAt, CreatedAt = now };
        a.Answers.Add(answer);
        return answer;
    }

    private static void Finish(VoiceRoomAggregate a, ExamQuestion q, DateTime at)
    {
        var answer = AnswerFor(a, q, at);
        answer.FinishedAt ??= at;
        answer.DurationSeconds = Math.Max(0, (int)(at - q.AskedAt!.Value).TotalSeconds);
        q.IsCompleted = true;
    }

    private VoiceRoomState Apply(VoiceRoomAggregate a, VoiceAnswerRequest? request, bool releaseHold = false)
    {
        ValidateOwner(a);
        if (a.Logs.Count == 0) throw new BusinessValidationException("Hãy vào phòng thi giọng nói từ lịch thi.");
        if (a.Questions.Count == 0) throw new BusinessValidationException("Lượt thi chưa được phân câu hỏi.");
        var now = Now;
        var awaiting = false;
        if (a.Candidate.Status != CandidateStatus.Completed)
        {
            if (now < SlotEnd(a)) ValidateWindow(a, now);
            else if (a.Candidate.Status != CandidateStatus.InProgress)
                throw new BusinessValidationException("Lượt thi đã bị hủy hoặc vắng thi.");

            // A probe is answered before anything else: while one is open the question
            // underneath it is already handed in and the next one has not started.
            var probe = OpenProbe(a);
            if (probe != null)
            {
                var probeDeadline = ProbeDeadline(a, probe);
                if (now >= probeDeadline) probe.AnsweredAt = probeDeadline;
                else if (request?.FollowUpId is int requested)
                {
                    if (requested != probe.FollowUpId)
                        throw new BusinessValidationException("Câu hỏi thêm này không còn mở.");
                    if (request.Transcript?.Length > 16000) throw new BusinessValidationException("Bản ghi lời quá dài.");
                    probe.AnswerTranscript = request.Transcript?.Trim();
                    if (request.Submit) probe.AnsweredAt = now;
                }
                probe = OpenProbe(a);
            }

            if (probe == null)
            {
                var current = a.Questions.FirstOrDefault(x => !x.IsCompleted);
                if (current != null)
                {
                    current.AskedAt ??= now < SlotEnd(a) ? now : SlotEnd(a);
                    // Expired answers are finalized before inspecting a client payload.
                    if (now >= Deadline(a, current)) Finish(a, current, Deadline(a, current));
                    else if (request != null && request.FollowUpId is null)
                    {
                        var target = a.Questions.SingleOrDefault(x => x.ExamQuestionId == request.ExamQuestionId)
                            ?? throw new BusinessValidationException("Câu hỏi không thuộc lượt thi.");
                        if (!target.IsCompleted && target != current)
                            throw new BusinessValidationException("Chưa đến câu hỏi này.");
                        if (target == current)
                        {
                            if (request.Transcript?.Length > 16000) throw new BusinessValidationException("Bản ghi lời quá dài.");
                            AnswerFor(a, current, now).Transcript = request.Transcript?.Trim();
                            if (request.Submit) Finish(a, current, now);
                        }
                    }
                }
                if (now >= SlotEnd(a))
                    foreach (var q in a.Questions.Where(x => !x.IsCompleted))
                    {
                        q.AskedAt ??= SlotEnd(a);
                        Finish(a, q, SlotEnd(a));
                    }

                // Hold the next question closed while the examiner decides. Its clock
                // would otherwise run while the student sits waiting for a probe.
                awaiting = !releaseHold && ProbeTarget(a, now) != null;
                var next = a.Questions.FirstOrDefault(x => !x.IsCompleted);
                if (next == null)
                {
                    if (!awaiting)
                    {
                        a.Candidate.Status = CandidateStatus.Completed;
                        a.Candidate.FinishedAt ??= now < SlotEnd(a) ? now : SlotEnd(a);
                    }
                }
                else if (!awaiting) next.AskedAt ??= now;
            }
        }

        var open = a.Candidate.Status == CandidateStatus.Completed ? null : OpenProbe(a);
        if (open != null)
        {
            var parent = ParentOf(a, open)!;
            return Snapshot(a, now, awaiting: false, new VoiceQuestion(parent.ExamQuestionId, parent.OrderNo,
                open.QuestionText, Stamp(open.AskedAt!.Value), Stamp(ProbeDeadline(a, open)),
                open.AnswerTranscript, open.AudioPath != null, open.FollowUpId, ReadReason(open.Reason)));
        }

        var current2 = a.Candidate.Status == CandidateStatus.Completed || awaiting
            ? null
            : a.Questions.FirstOrDefault(x => !x.IsCompleted);
        var saved = current2 == null ? null : a.Answers.SingleOrDefault(x => x.ExamQuestionId == current2.ExamQuestionId);
        return Snapshot(a, now, awaiting, current2 == null ? null : new VoiceQuestion(current2.ExamQuestionId,
            current2.OrderNo, current2.Question.QuestionText, Stamp(current2.AskedAt!.Value),
            Stamp(Deadline(a, current2)), saved?.Transcript, saved?.AudioPath != null));
    }

    private VoiceRoomState Snapshot(VoiceRoomAggregate a, DateTime now, bool awaiting, VoiceQuestion? question) =>
        new(a.Candidate.CandidateId, a.Candidate.ExamId, a.Candidate.Session.ExamName, Stamp(now), Stamp(SlotEnd(a)),
            a.Candidate.Status == CandidateStatus.Completed, a.Questions.Count(x => x.IsCompleted), a.Questions.Count,
            question,
            a.Questions.Where(x => x.IsCompleted).Select(q => {
                var answer = a.Answers.SingleOrDefault(x => x.ExamQuestionId == q.ExamQuestionId);
                var probes = answer == null ? [] : ProbesFor(a, answer.AnswerId)
                    .Where(x => x.AnsweredAt != null)
                    .Select(x => new VoiceSavedFollowUp(x.FollowUpId, x.QuestionText, x.AnswerTranscript,
                        x.AudioPath != null, ReadReason(x.Reason)))
                    .ToArray();
                return new VoiceSavedAnswer(q.ExamQuestionId, q.OrderNo, q.Question.QuestionText, answer?.Transcript,
                    answer?.AudioPath != null, probes);
            }).ToArray(),
            awaiting);

    /// <summary>Reads back what was stored in follow_up_questions.reason.</summary>
    internal static FollowUpReason ReadReason(string? reason) =>
        Enum.TryParse<FollowUpReason>(reason, ignoreCase: true, out var parsed) ? parsed : FollowUpReason.MissingPoint;

    private static DateTimeOffset Stamp(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Local));
}
