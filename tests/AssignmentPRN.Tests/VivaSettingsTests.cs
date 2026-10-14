using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AssignmentPRN.Tests;
public class VivaSettingsTests
{
    [Theory]
    [InlineData(14, 1)]
    [InlineData(1801, 1)]
    [InlineData(120, -1)]
    [InlineData(120, 6)]
    public async Task Invalid_limits_do_not_create_settings(int seconds, int count) {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        var repo = new ExamSessionRepository(db, new ExamStatePolicy());
        var service = new ExamVivaSettingsService(repo, new ExamVivaSettingsRepository(db, repo));
        Assert.False((await service.SaveAsync(1, new(seconds, count), null)).Success);
        Assert.Empty(await db.Set<ExamVivaSettings>().ToListAsync());
    }
    [Fact]
    public async Task Issued_paper_locks_settings_even_for_admin() {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        var repo = new ExamSessionRepository(db, new ExamStatePolicy());
        var service = new ExamVivaSettingsService(repo, new ExamVivaSettingsRepository(db, repo));
        Assert.False((await service.SaveAsync(1, new(60, 2), null)).Success);
        Assert.Empty(await db.Set<ExamVivaSettings>().ToListAsync());
    }
    [Fact]
    public async Task Owner_can_save_before_issuance_foreign_lecturer_cannot_and_started_session_locks() {
        await using var db = await IssuedPaperEditTests.OpenAsync();
        db.ExamQuestions.RemoveRange(db.ExamQuestions); await db.SaveChangesAsync();
        var session = await db.ExamSessions.SingleAsync();
        var repo = new ExamSessionRepository(db, new ExamStatePolicy());
        var service = new ExamVivaSettingsService(repo, new ExamVivaSettingsRepository(db, repo));
        Assert.False((await service.SaveAsync(1, new(60, 2), session.LecturerId + 1)).Success);
        Assert.True((await service.SaveAsync(1, new(60, 2), session.LecturerId)).Success);
        Assert.Equal(2, (await service.GetAsync(1, session.LecturerId)).Data!.MaxFollowUpsPerQuestion);
        session.Status = ExamSessionStatus.InProgress; await db.SaveChangesAsync();
        Assert.False((await service.SaveAsync(1, new(90, 3), null)).Success);
        Assert.Equal(60, (await db.Set<ExamVivaSettings>().AsNoTracking().SingleAsync()).SecondsPerQuestion);
    }
}
