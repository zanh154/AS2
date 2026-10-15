using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using CandidateStatus = AssignmentPRN.DataAccess.Enums.CandidateStatus;

namespace AssignmentPRN.Business.Services;

public sealed class VoiceRoomService(IVoiceRoomRepository repository, IQuestionService questions,
    TimeProvider clock) : IVoiceRoomService
{
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

    public Task<ServiceResponse<VoiceRoomState>> SyncAsync(int candidateId, int studentId,
        VoiceAnswerRequest? answer = null, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(() => repository.UpdateAsync(candidateId, studentId, a => Apply(a, answer), ct),
            "Không thể đồng bộ phòng thi. Hãy thử lại.");

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

    private VoiceRoomState Apply(VoiceRoomAggregate a, VoiceAnswerRequest? request)
    {
        ValidateOwner(a);
        if (a.Logs.Count == 0) throw new BusinessValidationException("Hãy vào phòng thi giọng nói từ lịch thi.");
        if (a.Questions.Count == 0) throw new BusinessValidationException("Lượt thi chưa được phân câu hỏi.");
        var now = Now;
        if (a.Candidate.Status != CandidateStatus.Completed)
        {
            if (now < SlotEnd(a)) ValidateWindow(a, now);
            else if (a.Candidate.Status != CandidateStatus.InProgress)
                throw new BusinessValidationException("Lượt thi đã bị hủy hoặc vắng thi.");
            var current = a.Questions.FirstOrDefault(x => !x.IsCompleted);
            if (current != null)
            {
                current.AskedAt ??= now < SlotEnd(a) ? now : SlotEnd(a);
                // Expired answers are finalized before inspecting a client payload.
                if (now >= Deadline(a, current)) Finish(a, current, Deadline(a, current));
                else if (request != null)
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
            var next = a.Questions.FirstOrDefault(x => !x.IsCompleted);
            if (next == null)
            {
                a.Candidate.Status = CandidateStatus.Completed;
                a.Candidate.FinishedAt ??= now < SlotEnd(a) ? now : SlotEnd(a);
            }
            else next.AskedAt ??= now;
        }
        var open = a.Candidate.Status == CandidateStatus.Completed ? null : a.Questions.FirstOrDefault(x => !x.IsCompleted);
        var saved = open == null ? null : a.Answers.SingleOrDefault(x => x.ExamQuestionId == open.ExamQuestionId);
        return new(a.Candidate.CandidateId, a.Candidate.ExamId, a.Candidate.Session.ExamName, Stamp(now), Stamp(SlotEnd(a)),
            a.Candidate.Status == CandidateStatus.Completed, a.Questions.Count(x => x.IsCompleted), a.Questions.Count,
            open == null ? null : new(open.ExamQuestionId, open.OrderNo, open.Question.QuestionText,
                Stamp(open.AskedAt!.Value), Stamp(Deadline(a, open)), saved?.Transcript, saved?.AudioPath != null));
    }

    private static DateTimeOffset Stamp(DateTime date) => new(DateTime.SpecifyKind(date, DateTimeKind.Local));
}
