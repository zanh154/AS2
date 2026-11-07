using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>Called inside the transaction that changes candidate states.</summary>
internal static class ExamSessionLifecycle
{
    internal static async Task SynchronizeAsync(AivesDbContext context, IExamStatePolicy policy, int examId, DateTime now, CancellationToken ct)
    {
        var session = await context.ExamSessions.SingleAsync(x => x.ExamId == examId, ct);
        var statuses = await context.ExamCandidates.Where(x => x.ExamId == examId).Select(x => x.Status).ToListAsync(ct);
        var next = policy.SessionAfterProgress(session.Status, statuses);
        if (next == session.Status) return;
        session.Status = next;
        session.UpdatedAt = now;
        await context.SaveChangesAsync(ct);
    }
}
