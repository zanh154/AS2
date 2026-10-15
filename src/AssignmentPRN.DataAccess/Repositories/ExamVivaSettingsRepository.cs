using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Contracts;
using Microsoft.EntityFrameworkCore;
namespace AssignmentPRN.DataAccess.Repositories;
public interface IExamVivaSettingsRepository
{
    Task<ExamVivaSettings?> GetAsync(int examId, CancellationToken ct);
    Task SaveAsync(int examId, int seconds, int maxFollowUps, Action<ExamSessionDetail, bool> validate, CancellationToken ct);
}
public sealed class ExamVivaSettingsRepository(AivesDbContext db, IExamSessionRepository sessions) : IExamVivaSettingsRepository
{
    public Task<ExamVivaSettings?> GetAsync(int examId, CancellationToken ct) =>
        db.Set<ExamVivaSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.ExamId == examId, ct);
    public async Task SaveAsync(int examId, int seconds, int maxFollowUps, Action<ExamSessionDetail, bool> validate, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var state = await sessions.GetDetailAsync(examId, ct) ?? throw new KeyNotFoundException("Không tìm thấy phiên thi.");
        var issued = await db.ExamQuestions.AnyAsync(x => x.Candidate.ExamId == examId, ct);
        validate(state, issued);
        var row = await db.Set<ExamVivaSettings>().SingleOrDefaultAsync(x => x.ExamId == examId, ct);
        if (row is null) { row = new() { ExamId = examId }; db.Add(row); }
        row.SecondsPerQuestion = seconds;
        row.MaxFollowUpsPerQuestion = maxFollowUps;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
