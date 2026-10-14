using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using B = AssignmentPRN.Business.Interfaces;
using BSvc = AssignmentPRN.Business.Services;
using Input = AssignmentPRN.DataAccess.Contracts.ExamSessionUpdateInput;

namespace AssignmentPRN.Tests;

public class IssuedPaperEditTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Issued_paper_blocks_changes_in_service_and_repository(bool course, bool count)
    {
        await using var db = await OpenAsync();
        var service = new BSvc.ExamSessionService(new ExamSessionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()), new CatalogRepository(db), new QuestionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()));
        var result = await service.UpdateAsync(new B.ExamSessionUpdateInput {
            ExamId = 1, CourseId = course ? 2 : 1, MainQuestionCount = count ? 2 : 1,
            ExamName = "Changed", StartTime = Start, TimePerStudent = 10 }, 1);
        Assert.False(result.Success);
        Assert.Contains("huỷ đề", result.Error);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => new ExamSessionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()).UpdateAsync(Request(course, count)));
        Assert.Contains("huỷ đề", error.Message);
        db.ChangeTracker.Clear();
        var saved = await db.ExamSessions.SingleAsync();
        Assert.Equal(1, saved.CourseId);
        Assert.Equal(1, saved.MainQuestionCount);
        Assert.Single(await db.ExamQuestions.ToListAsync());
    }

    [Fact]
    public async Task Issued_paper_allows_other_details_to_change()
    {
        await using var db = await OpenAsync();
        var saved = await new ExamSessionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()).UpdateAsync(Request(false, false));
        Assert.Equal("Changed", saved.ExamName);
        Assert.Single(await db.ExamQuestions.ToListAsync());
    }

    [Fact]
    public async Task Clearing_unstarted_paper_allows_configuration_change()
    {
        await using var db = await OpenAsync();
        var questions = new BSvc.QuestionService(new QuestionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()), new CatalogRepository(db), null!);
        Assert.True((await questions.ClearExamAssignmentAsync(1)).Success);
        var saved = await new ExamSessionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()).UpdateAsync(Request(true, true));
        Assert.Equal(2, saved.Course.CourseId);
        Assert.Equal(2, saved.MainQuestionCount);
    }

    [Fact]
    public async Task Started_candidate_cannot_clear_paper()
    {
        await using var db = await OpenAsync();
        var candidate = await db.ExamCandidates.SingleAsync();
        candidate.Status = CandidateStatus.InProgress;
        candidate.StartedAt = DateTime.Now;
        await db.SaveChangesAsync();
        var questions = new BSvc.QuestionService(new QuestionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()), new CatalogRepository(db), null!);
        Assert.False((await questions.ClearExamAssignmentAsync(1)).Success);
        Assert.Single(await db.ExamQuestions.ToListAsync());
    }

    private static DateTime Start => DateTime.Today.AddDays(10).AddHours(8);
    private static Input Request(bool course, bool count) => new() {
        ExamId = 1, CourseId = course ? 2 : 1, MainQuestionCount = count ? 2 : 1,
        ExamName = "Changed", StartTime = Start, TimePerStudent = 10 };

    internal static async Task<AivesDbContext> OpenAsync()
    {
        var db = new AivesDbContext(new DbContextOptionsBuilder<AivesDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Roles.Add(new Role { RoleId = 1, RoleName = "Lecturer" });
        db.Users.Add(new User { UserId = 1, RoleId = 1, FullName = "Test", Email = "test@example.test", PasswordHash = "test", Status = "Active" });
        db.Courses.AddRange(Enumerable.Range(1, 2).Select(id => new Course {
            CourseId = id, CourseCode = $"C{id}", CourseName = $"Course {id}", LecturerId = 1, IsActive = true }));
        db.ExamSessions.Add(new ExamSession { ExamId = 1, CourseId = 1, LecturerId = 1,
            ExamName = "Original", StartTime = Start, EndTime = Start.AddMinutes(10),
            TimePerStudent = 10, MainQuestionCount = 1, Status = ExamSessionStatus.Scheduled });
        db.ExamCandidates.Add(new ExamCandidate { CandidateId = 1, ExamId = 1, StudentId = 1,
            ScheduledTime = Start, Status = CandidateStatus.Waiting });
        db.Questions.Add(new Question { QuestionId = 1, CourseId = 1, CreatedBy = 1, QuestionText = "Test" });
        db.ExamQuestions.Add(new ExamQuestion { ExamId = 1, CandidateId = 1, QuestionId = 1, OrderNo = 1 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}
