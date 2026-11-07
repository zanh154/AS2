using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>Reads marked papers for the examiner and closes slots whose time is up.</summary>
public interface IExamResultRepository
{
    Task<ExamResultSession?> GetSessionAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Every candidate of the session, in slot order, with their graded slots.</summary>
    Task<IReadOnlyList<CandidateResultRow>> ListCandidatesAsync(
        int examId,
        CancellationToken cancellationToken = default);

    Task<CandidateResultRow?> GetCandidateAsync(int candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the given slots to their new status in one transaction. A slot that reaches
    /// Completed or Absent is stamped with <paramref name="finishedAt"/>.
    /// </summary>
    Task UpdateCandidateStatusesAsync(
        IReadOnlyDictionary<int, CandidateStatus> statusByCandidate,
        DateTime finishedAt,
        CancellationToken cancellationToken = default);
}

public class ExamResultRepository(AivesDbContext context, IExamStatePolicy policy) : IExamResultRepository
{
    public Task<ExamResultSession?> GetSessionAsync(int examId, CancellationToken cancellationToken = default) =>
        context.ExamSessions
            .AsNoTracking()
            .Where(session => session.ExamId == examId)
            .Select(session => new ExamResultSession
            {
                ExamId = session.ExamId,
                ExamName = session.ExamName,
                CourseCode = session.Course.CourseCode,
                CourseName = session.Course.CourseName,
                LecturerId = session.LecturerId,
                LecturerName = session.Lecturer.FullName,
                StartTime = session.StartTime,
                TimePerStudent = session.TimePerStudent,
                MainQuestionCount = session.MainQuestionCount,
                MaxFollowUpCount = session.MaxFollowUpCount,
                Status = session.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CandidateResultRow>> ListCandidatesAsync(
        int examId,
        CancellationToken cancellationToken = default) =>
        await Candidates()
            .Where(candidate => candidate.ExamId == examId)
            .OrderBy(candidate => candidate.ScheduledTime)
            .ThenBy(candidate => candidate.CandidateId)
            .ToListAsync(cancellationToken);

    public Task<CandidateResultRow?> GetCandidateAsync(int candidateId, CancellationToken cancellationToken = default) =>
        Candidates().FirstOrDefaultAsync(candidate => candidate.CandidateId == candidateId, cancellationToken);

    public async Task UpdateCandidateStatusesAsync(
        IReadOnlyDictionary<int, CandidateStatus> statusByCandidate,
        DateTime finishedAt,
        CancellationToken cancellationToken = default)
    {
        if (statusByCandidate.Count == 0)
        {
            return;
        }

        var ids = statusByCandidate.Keys.ToList();
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var candidates = await context.ExamCandidates.Include(x => x.Session)
            .Where(candidate => ids.Contains(candidate.CandidateId))
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            // The student may have submitted since the service calculated overdue slots.
            var next = policy.CloseOverdue(candidate.Session.Status,
                candidate.Status, candidate.ScheduledTime, candidate.Session.TimePerStudent, finishedAt);
            if (next is null) continue;
            candidate.Status = next.Value;
            candidate.FinishedAt ??= finishedAt;
        }

        await context.SaveChangesAsync(cancellationToken);
        foreach (var examId in candidates.Select(x => x.ExamId).Distinct())
            await ExamSessionLifecycle.SynchronizeAsync(context, policy, examId, finishedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Candidates with their slots graded in the database: a slot is right when the stored
    /// choice is the option marked correct.
    /// </summary>
    private IQueryable<CandidateResultRow> Candidates() =>
        context.ExamCandidates
            .AsNoTracking()
            .Select(candidate => new CandidateResultRow
            {
                CandidateId = candidate.CandidateId,
                ExamId = candidate.ExamId,
                StudentName = candidate.Student.FullName,
                StudentEmail = candidate.Student.Email,
                ScheduledTime = candidate.ScheduledTime,
                Status = candidate.Status,
                StartedAt = candidate.StartedAt,
                FinishedAt = candidate.FinishedAt,
                Slots = context.ExamQuestions
                    .Where(slot => slot.CandidateId == candidate.CandidateId)
                    .OrderBy(slot => slot.OrderNo)
                    .Select(slot => new GradedSlot
                    {
                        IsFollowUp = slot.ParentExamQuestionId != null,
                        IsAnswered = context.Answers.Any(answer =>
                            answer.ExamQuestionId == slot.ExamQuestionId && answer.SelectedOptionId != null),
                        IsCorrect = context.Answers.Any(answer =>
                            answer.ExamQuestionId == slot.ExamQuestionId
                            && answer.SelectedOption != null
                            && answer.SelectedOption.IsCorrect)
                    })
                    .ToList()
            });
}
