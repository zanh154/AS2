using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using B = AssignmentPRN.Business.Interfaces;
using BSvc = AssignmentPRN.Business.Services;

namespace AssignmentPRN.Tests;

public class SharedQuestionConfigurationTests
{
    private static BSvc.QuestionService Service(AivesDbContext db) =>
        new(new QuestionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()), new CatalogRepository(db), null!);
    private static B.ExamQuestionAssignmentRequest Request(int count = 1, bool filtered = true) => new() {
        ExamId = 1, CourseId = 1, CountPerCandidate = count,
        MaterialIds = filtered ? [1] : [], Difficulties = filtered ? [B.QuestionDifficulty.Easy] : [] };

    [Fact]
    public async Task Manual_configuration_survives_reload_and_is_used_for_later_student()
    {
        await using var db = await SeedAsync();
        Assert.True((await Service(db).AssignToExamAsync(Request())).Success);
        db.ChangeTracker.Clear();
        var config = (await Service(db).GetExamConfigurationAsync(1)).Data!;
        Assert.Equal(1, config.Count);
        Assert.Equal(new[] { 1 }, config.MaterialIds);
        Assert.Equal(new[] { B.QuestionDifficulty.Easy }, config.Difficulties);
        db.Users.Add(new User { UserId = 2, RoleId = 1, FullName = "Later", Email = "later@example.test", PasswordHash = "x", Status = "Active" });
        db.ExamCandidates.Add(new ExamCandidate { CandidateId = 2, ExamId = 1, StudentId = 2,
            ScheduledTime = DateTime.Now.AddMinutes(-1), Status = CandidateStatus.Waiting });
        await db.SaveChangesAsync();
        var result = await Service(db).EnterExamAsync(2, 2);
        Assert.True(result.Success, result.Error);
        var questions = await db.ExamQuestions.Include(x => x.Question).ToListAsync();
        Assert.Equal(2, questions.Count);
        Assert.Equal(2, questions.Select(x => x.QuestionId).Distinct().Count());
        Assert.All(questions, x => {
            Assert.Equal(1, x.Question.SourceMaterialId);
            Assert.Equal(QuestionDifficulty.Easy, x.Question.Difficulty);
        });
    }

    [Fact]
    public async Task Forged_count_cannot_override_session_count()
    {
        await using var db = await SeedAsync();
        var result = await Service(db).AssignToExamAsync(Request(2));
        Assert.False(result.Success);
        Assert.Empty(await db.ExamQuestions.ToListAsync());
        Assert.Null((await db.ExamSessions.SingleAsync()).QuestionScopeJson);
    }

    [Fact]
    public async Task Existing_papers_freeze_scope_until_cleared()
    {
        await using var db = await SeedAsync();
        Assert.True((await Service(db).AssignToExamAsync(Request())).Success);
        Assert.False((await Service(db).AssignToExamAsync(Request(filtered: false))).Success);
        Assert.Equal(new[] { 1 }, (await Service(db).GetExamConfigurationAsync(1)).Data!.MaterialIds);
        Assert.True((await Service(db).ClearExamAssignmentAsync(1)).Success);
        Assert.True((await Service(db).AssignToExamAsync(Request(filtered: false))).Success);
        Assert.Empty((await Service(db).GetExamConfigurationAsync(1)).Data!.MaterialIds);
    }

    [Fact]
    public async Task Failed_draw_does_not_save_scope_or_partial_papers()
    {
        await using var db = await SeedAsync();
        var session = await db.ExamSessions.SingleAsync();
        session.MainQuestionCount = 3;
        await db.SaveChangesAsync();
        Assert.False((await Service(db).AssignToExamAsync(Request(3))).Success);
        db.ChangeTracker.Clear();
        Assert.Null((await db.ExamSessions.SingleAsync()).QuestionScopeJson);
        Assert.Empty(await db.ExamQuestions.ToListAsync());
    }

    [Fact]
    public async Task Adding_student_checks_remaining_questions_in_saved_scope()
    {
        await using var db = await SeedAsync();
        Assert.True((await Service(db).AssignToExamAsync(Request())).Success);
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Student" });
        db.Users.AddRange(Enumerable.Range(2, 2).Select(id => new User {
            UserId = id, RoleId = 3, FullName = $"Student {id}", Email = $"s{id}@example.test", PasswordHash = "x", Status = "Active" }));
        db.AcademicClasses.Add(new AcademicClass { ClassId = 1, ClassCode = "TEST", ClassName = "Test",
            CourseId = 1, LecturerId = 1, IsActive = true,
            Students = [new ClassStudent { StudentId = 2 }, new ClassStudent { StudentId = 3 }] });
        await db.SaveChangesAsync();
        var sessions = new BSvc.ExamSessionService(new ExamSessionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()), new CatalogRepository(db), new QuestionRepository(db, new AssignmentPRN.Business.Policies.ExamStatePolicy()));
        var start = (await db.ExamSessions.SingleAsync()).StartTime;
        Assert.True((await sessions.AddStudentAsync(1, "s2@example.test", start.AddMinutes(10))).Success);
        // Only one unassigned Easy/topic-1 question remains: reserved for student 2.
        Assert.False((await sessions.AddStudentAsync(1, "s3@example.test", start.AddMinutes(20))).Success);
        Assert.Equal(2, await db.ExamCandidates.CountAsync());
    }

    [Fact]
    public async Task Foreign_topic_is_rejected_without_saving_configuration()
    {
        await using var db = await SeedAsync();
        var material = await db.CourseMaterials.SingleAsync(x => x.MaterialId == 2);
        material.CourseId = 2;
        await db.SaveChangesAsync();
        var result = await Service(db).AssignToExamAsync(new B.ExamQuestionAssignmentRequest {
            ExamId = 1, CourseId = 1, CountPerCandidate = 1, MaterialIds = [2] });
        Assert.False(result.Success);
        Assert.Empty(await db.ExamQuestions.ToListAsync());
    }

    private static async Task<AivesDbContext> SeedAsync()
    {
        var db = await IssuedPaperEditTests.OpenAsync();
        db.ExamQuestions.RemoveRange(db.ExamQuestions);
        db.CourseMaterials.AddRange(Enumerable.Range(1, 2).Select(id => new CourseMaterial {
            MaterialId = id, CourseId = 1, FileName = $"Topic{id}.pdf", FilePath = $"/test{id}.pdf", UploadedBy = 1 }));
        var first = await db.Questions.SingleAsync();
        first.SourceMaterialId = 1;
        first.Difficulty = QuestionDifficulty.Easy;
        db.Questions.AddRange(new Question { QuestionId = 2, CourseId = 1, CreatedBy = 1,
            SourceMaterialId = 1, QuestionText = "Easy same topic", Difficulty = QuestionDifficulty.Easy },
            new Question { QuestionId = 3, CourseId = 1, CreatedBy = 1,
            SourceMaterialId = 2, QuestionText = "Hard other topic", Difficulty = QuestionDifficulty.Hard });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}
