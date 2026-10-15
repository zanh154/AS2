using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Contracts;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IExamSessionRepository
{
    Task<ExamSessionDetail> UpdateAsync(ExamSessionUpdateInput input, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(int examId, AssignmentPRN.DataAccess.Enums.ExamSessionStatus status, CancellationToken cancellationToken = default);

    Task<ExamStudentSearchResult> SearchExamStudentsAsync(ExamStudentSearch filter,
        int? lecturerId, CancellationToken cancellationToken = default);
    Task RemoveStudentAsync(int examId, int candidateId, CancellationToken cancellationToken = default);

    Task<ExamSessionDetail> AddStudentAsync(int examId, int studentId, DateTime scheduledTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExamSessionListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<ExamSessionDetail?> GetDetailAsync(int examId, CancellationToken cancellationToken = default);

    Task<ExamSessionDetail?> GetDetailByCandidateIdAsync(
        int candidateId,
        CancellationToken cancellationToken = default);

    Task<StudentSchedule?> GetStudentScheduleAsync(int studentUserId, CancellationToken cancellationToken = default);

    Task<ExamSessionDetail> CreateAsync(ExamSessionAggregateInput input, CancellationToken cancellationToken = default);

    /// <summary>Moves one candidate's slot, keeping the session's per-student duration.</summary>
    Task<ExamSessionDetail> RescheduleAsync(
        int candidateId,
        DateTime scheduledTime,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int examId, CancellationToken cancellationToken = default);
}
