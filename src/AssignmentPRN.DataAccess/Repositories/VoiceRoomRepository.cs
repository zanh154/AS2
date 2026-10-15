using System.Data;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <param name="FollowUps">
/// Probes the AI examiner asked on this candidate's answers, oldest first. Empty for a
/// session that does not use them.
/// </param>
/// <param name="CriteriaByQuestion">
/// Rubric criteria of each question in the paper, so the analyzer can be told what the
/// answer is being judged on without reaching into a repository itself. A question with
/// no rubric is absent.
/// </param>
public sealed record VoiceRoomAggregate(ExamCandidate Candidate, List<ExamQuestion> Questions,
    List<Answer> Answers, ExamVivaSettings Settings, List<ExamLog> Logs,
    List<FollowUpQuestion> FollowUps, IReadOnlyDictionary<int, List<RubricCriteria>> CriteriaByQuestion);

public interface IVoiceRoomRepository
{
    Task<bool> IsVoiceAsync(int candidateId, int studentId, CancellationToken ct);
    Task<T> UpdateAsync<T>(int candidateId, int studentId, Func<VoiceRoomAggregate, T> apply, CancellationToken ct);
}

/// <summary>Serializes question transitions, drafts and submissions across server instances.</summary>
public sealed class VoiceRoomRepository(AivesDbContext db, IExamStatePolicy policy) : IVoiceRoomRepository
{
    public const string StartedAction = "voice-room.started";

    public Task<bool> IsVoiceAsync(int candidateId, int studentId, CancellationToken ct) =>
        db.Set<ExamLog>().AnyAsync(x => x.CandidateId == candidateId && x.UserId == studentId
            && x.Action == StartedAction, ct);

    public async Task<T> UpdateAsync<T>(int candidateId, int studentId, Func<VoiceRoomAggregate, T> apply, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        db.ChangeTracker.Clear();
        // Lock before reading mutable state. Parameters remain SQL parameters.
        if (db.Database.IsMySql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT candidate_id FROM exam_candidates WHERE candidate_id = {candidateId} AND student_id = {studentId} FOR UPDATE", ct);
        var candidate = await db.ExamCandidates.Include(x => x.Session).Include(x => x.Student).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.StudentId == studentId, ct)
            ?? throw new ArgumentException("Không có quyền truy cập lượt thi.");
        var questions = await db.ExamQuestions.Include(x => x.Question)
            .Where(x => x.CandidateId == candidateId && x.ExamId == candidate.ExamId)
            .OrderBy(x => x.OrderNo).ThenBy(x => x.ExamQuestionId).ToListAsync(ct);
        var answers = await db.Answers.Where(x => x.CandidateId == candidateId).ToListAsync(ct);
        var settings = await db.Set<ExamVivaSettings>().SingleOrDefaultAsync(x => x.ExamId == candidate.ExamId, ct)
            ?? new ExamVivaSettings { ExamId = candidate.ExamId };
        var logs = await db.Set<ExamLog>().Where(x => x.CandidateId == candidateId && x.Action == StartedAction).ToListAsync(ct);
        var followUps = await db.FollowUpQuestions
            .Where(x => db.Answers.Any(answer => answer.AnswerId == x.AnswerId && answer.CandidateId == candidateId))
            .OrderBy(x => x.AnswerId).ThenBy(x => x.OrderNo).ThenBy(x => x.FollowUpId)
            .ToListAsync(ct);
        var questionIds = questions.Select(x => x.QuestionId).Distinct().ToList();
        var criteria = await db.Rubrics
            .Where(rubric => questionIds.Contains(rubric.QuestionId))
            .Select(rubric => new { rubric.QuestionId, Criteria = rubric.Criteria.OrderBy(x => x.DisplayOrder).ToList() })
            .ToListAsync(ct);

        T result;
        try
        {
            result = apply(new(candidate, questions, answers, settings, logs, followUps,
                criteria.ToDictionary(x => x.QuestionId, x => x.Criteria)));
        }
        catch { db.ChangeTracker.Clear(); throw; }

        foreach (var answer in answers.Where(x => x.AnswerId == 0)) db.Answers.Add(answer);
        foreach (var log in logs.Where(x => x.LogId == 0)) db.Set<ExamLog>().Add(log);
        // A probe always hangs off an answer that was committed in an earlier transaction,
        // so its AnswerId is already real here.
        foreach (var followUp in followUps.Where(x => x.FollowUpId == 0)) db.FollowUpQuestions.Add(followUp);
        await db.SaveChangesAsync(ct);
        await ExamSessionLifecycle.SynchronizeAsync(db, policy, candidate.ExamId, DateTime.Now, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
