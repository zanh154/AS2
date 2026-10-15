using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// The examiner's side of the results module: the score sheet of a session, the marked
/// paper of one candidate, and closing slots whose time is over. Ownership is checked
/// here, not in the controller: a lecturer only reaches the sessions they run.
/// </summary>
public class ExamResultService(
    IExamResultRepository resultRepository,
    IQuestionRepository questionRepository, IExamRealtimeNotifier? realtime = null, IExamSessionRepository? sessions = null,
    GradingQueue? grading = null) : IExamResultService
{
    public Task<ServiceResponse<SessionResultResponse>> GetSessionResultsAsync(
        int examId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var session = await LoadOwnedSessionAsync(examId, lecturerId, cancellationToken);
                var candidates = await resultRepository.ListCandidatesAsync(session.ExamId, cancellationToken);
                var overdue = CloseOverdueSlots(ToSlotStates(candidates), session.TimePerStudent, DateTime.Now);

                return new SessionResultResponse
                {
                    ExamId = session.ExamId,
                    ExamName = session.ExamName,
                    CourseCode = session.CourseCode,
                    CourseName = session.CourseName,
                    LecturerName = session.LecturerName,
                    StartTime = session.StartTime,
                    TimePerStudent = session.TimePerStudent,
                    MainQuestionCount = session.MainQuestionCount,
                    MaxFollowUpCount = session.MaxFollowUpCount,
                    Status = session.Status.ToBusiness(),
                    Candidates = candidates.Select(MapRow).ToList(),
                    OverdueCount = CanClose(session.Status.ToBusiness()) ? overdue.Count : 0
                };
            },
            "Không thể tải kết quả phiên thi.");
    }

    public Task<ServiceResponse<CandidateResultResponse>> GetCandidateResultAsync(
        int candidateId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var id = BusinessValidation.PositiveId(candidateId, "lượt thi");
                var candidate = await resultRepository.GetCandidateAsync(id, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy lượt thi.");
                var session = await LoadOwnedSessionAsync(candidate.ExamId, lecturerId, cancellationToken);

                var questions = await questionRepository.ListCandidateResultsAsync(
                    id, includeExpectedAnswer: true, cancellationToken);

                return new CandidateResultResponse
                {
                    ExamId = session.ExamId,
                    StudentName = candidate.StudentName,
                    StudentEmail = candidate.StudentEmail,
                    Status = candidate.Status.ToBusiness(),
                    ScheduledTime = candidate.ScheduledTime,
                    StartedAt = candidate.StartedAt,
                    FinishedAt = candidate.FinishedAt,
                    Paper = new ExamResultResponse
                    {
                        CandidateId = candidate.CandidateId,
                        ExamName = session.ExamName,
                        CourseCode = session.CourseCode,
                        CourseName = session.CourseName,
                        LecturerName = session.LecturerName,
                        Questions = questions.Select(item => item.ToBusiness()).ToList()
                    }
                };
            },
            "Không thể tải bài làm của sinh viên.");
    }

    public Task<ServiceResponse<OverdueSlotsResult>> CloseOverdueSlotsAsync(
        int examId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var session = await LoadOwnedSessionAsync(examId, lecturerId, cancellationToken);
                if (!CanClose(session.Status.ToBusiness()))
                {
                    throw new BusinessValidationException(
                        "Chỉ chốt được ca thi của phiên đã xếp lịch hoặc đang diễn ra.");
                }

                var now = DateTime.Now;
                var candidates = await resultRepository.ListCandidatesAsync(session.ExamId, cancellationToken);
                var changes = CloseOverdueSlots(ToSlotStates(candidates), session.TimePerStudent, now);
                if (changes.Count == 0)
                {
                    throw new BusinessValidationException("Không có ca thi nào đã hết giờ cần chốt.");
                }

                await resultRepository.UpdateCandidateStatusesAsync(
                    changes.ToDictionary(item => item.Key, item => item.Value.ToDataAccess()),
                    now,
                    cancellationToken);
                // A slot the lecturer closes was never handed in by the student, so
                // nothing queued it for marking. Without this it would wait for the
                // next restart to be swept up.
                if (grading is not null)
                {
                    foreach (var closed in changes.Where(x => x.Value == CandidateStatus.Completed))
                    {
                        try { await grading.QueueCandidateAsync(closed.Key, cancellationToken); }
                        catch (Exception) { /* The startup sweep still finds it. */ }
                    }
                }
                if (realtime is not null && sessions is not null) {
                    var updated = await sessions.GetDetailAsync(examId, cancellationToken);
                    if (updated is not null) await realtime.ChangedAsync(new(examId, updated.Lecturer.UserId, updated.Candidates.Select(x => x.StudentId).ToArray(), "closed-overdue"), cancellationToken);
                }
                return new OverdueSlotsResult
                {
                    MarkedAbsent = changes.Values.Count(status => status == CandidateStatus.Absent),
                    Completed = changes.Values.Count(status => status == CandidateStatus.Completed)
                };
            },
            "Không thể chốt các ca thi đã hết giờ.");
    }

    /// <summary>
    /// Which slots of a session may be closed now that their time is over:
    /// <list type="bullet">
    /// <item>a student still Waiting when their slot ended never showed up: Absent;</item>
    /// <item>a student still In progress after the slot and its submit grace left without
    /// handing in: Completed, marked on what was saved (nothing saved scores zero).</item>
    /// </list>
    /// Without this a session could never be completed, since completing requires that no
    /// student is left Waiting or In progress. Takes <paramref name="now"/> as an argument
    /// and touches no repository, so it can be exercised on its own.
    /// </summary>
    internal static IReadOnlyDictionary<int, CandidateStatus> CloseOverdueSlots(
        IEnumerable<SlotState> slots,
        int minutesPerStudent,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var result = new Dictionary<int, CandidateStatus>();
        foreach (var slot in slots)
        {
            var next = ExamLifecycleRules.CloseOverdue(slot.Status, slot.ScheduledTime, minutesPerStudent, now);
            if (next.HasValue)
            {
                result[slot.CandidateId] = next.Value;
            }
        }

        return result;
    }

    /// <summary>A cancelled, drafted or finished session is left as it is.</summary>
    private static bool CanClose(ExamSessionStatus status) => ExamSessionRules.CanSit(status);

    private async Task<ExamResultSession> LoadOwnedSessionAsync(
        int examId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        var id = BusinessValidation.PositiveId(examId, "phiên thi");
        var session = await resultRepository.GetSessionAsync(id, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");

        if (lecturerId.HasValue && session.LecturerId != lecturerId.Value)
        {
            throw new BusinessValidationException("Bạn không có quyền xem kết quả của phiên thi này.");
        }

        return session;
    }

    private static IEnumerable<SlotState> ToSlotStates(IEnumerable<CandidateResultRow> candidates) =>
        candidates.Select(candidate => new SlotState(
            candidate.CandidateId,
            candidate.Status.ToBusiness(),
            candidate.ScheduledTime));

    private static CandidateResultItemResponse MapRow(CandidateResultRow candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        StudentName = candidate.StudentName,
        StudentEmail = candidate.StudentEmail,
        ScheduledTime = candidate.ScheduledTime,
        Status = candidate.Status.ToBusiness(),
        MainCorrect = candidate.Slots.Count(slot => !slot.IsFollowUp && slot.IsCorrect),
        MainTotal = candidate.Slots.Count(slot => !slot.IsFollowUp),
        FollowUpCorrect = candidate.Slots.Count(slot => slot.IsFollowUp && slot.IsCorrect),
        FollowUpTotal = candidate.Slots.Count(slot => slot.IsFollowUp),
        Score = candidate.Status.ToBusiness() == CandidateStatus.Completed
            ? ExamScoring.Score(candidate.Slots.Select(slot => (slot.IsFollowUp, slot.IsCorrect)))
            : null
    };
}

/// <summary>A slot as the overdue check needs to see it.</summary>
public sealed record SlotState(int CandidateId, CandidateStatus Status, DateTime? ScheduledTime);
