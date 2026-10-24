using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Da = AssignmentPRN.DataAccess.Entities;
using DocumentStatus = AssignmentPRN.Business.Interfaces.DocumentStatus;
using GeneratedQuestionStatus = AssignmentPRN.Business.Interfaces.GeneratedQuestionStatus;
using DocumentBloomLevel = AssignmentPRN.Business.Interfaces.DocumentBloomLevel;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Tests;

public partial class DocumentOperationalTests
{
    [Theory]
    [InlineData(2, "Lecturer")]
    [InlineData(3, "Student")]
    [InlineData(1, "Admin")]
    public async Task Foreign_owner_student_and_forged_role_cannot_read_generate_review_or_delete(int id, string role)
    {
        await using var db = await OpenAsync();
        var service = Service(db, id, role);
        Assert.False((await service.GetAsync(1)).Success);
        Assert.False((await service.ListQuestionsByDocumentAsync(1)).Success);
        Assert.False((await service.GenerateQuestionsAsync(1, new(1, null, null))).Success);
        Assert.False((await service.ReviewQuestionAsync(1, id, new(GeneratedQuestionStatus.Approved, null))).Success);
        Assert.False((await service.DeleteAsync(1, id)).Success);
        Assert.Single(await db.Documents.ToListAsync());
        Assert.Equal(Da.GeneratedQuestionStatus.PendingReview, (await db.GeneratedQuestions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approval_saves_edits_and_cannot_be_replaced_by_a_second_decision()
    {
        await using var db = await OpenAsync();
        var service = Service(db);
        Assert.True((await service.ReviewQuestionAsync(1, 1, new(GeneratedQuestionStatus.Approved, "checked", "Edited", "Answer", "Reason", DocumentBloomLevel.Analyze, "OOP"))).Success);
        db.ChangeTracker.Clear();
        var question = await db.GeneratedQuestions.SingleAsync();
        Assert.Equal("Edited", question.QuestionText);
        Assert.Equal("Answer", question.AnswerKey);
        Assert.Equal("Reason", question.Explanation);
        Assert.Equal(Da.DocumentBloomLevel.Analyze, question.BloomLevel);
        Assert.Equal("OOP", question.Topic);
        Assert.Equal(1, question.ReviewedById);
        Assert.NotNull(question.ReviewedAt);
        Assert.False((await service.ReviewQuestionAsync(1, 1, new(GeneratedQuestionStatus.Rejected, "second"))).Success);
        Assert.Equal(Da.GeneratedQuestionStatus.Approved, (await db.GeneratedQuestions.AsNoTracking().SingleAsync()).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Invalid_generation_count_is_rejected_before_calling_AI(int count)
    {
        await using var db = await OpenAsync();
        var result = await Service(db).GenerateQuestionsAsync(1, new(count, null, null));
        Assert.False(result.Success);
        Assert.Contains("Số câu hỏi", result.Error);
    }

    [Fact]
    public async Task Rag_retrieval_stays_in_selected_document_and_processing_retry_replaces_chunks()
    {
        await using var db = await OpenAsync();
        db.Documents.Add(new Document { DocumentId = 2, CourseId = 1, UploadedById = 1, FileName = "second.docx", StoragePath = "second.docx" });
        db.DocumentChunks.AddRange(new DocumentChunk { DocumentId = 1, ChunkIndex = 0, Content = "Selected document", EmbeddingJson = "[0.8,0.6]" }, new DocumentChunk { DocumentId = 2, ChunkIndex = 0, Content = "Different document", EmbeddingJson = "[1,0]" });
        await db.SaveChangesAsync();
        var repo = new DocumentRepository(db);
        var matches = await repo.SearchSimilarChunksAsync([1, 0], 1, 5, -1, documentId: 1);
        Assert.Single(matches);
        Assert.Equal("Selected document", matches[0].Content);
        await repo.AddChunksAsync([new DocumentChunk { DocumentId = 1, ChunkIndex = 0, Content = "Retried content", EmbeddingJson = "[1,0]" }]);
        Assert.Single(await repo.GetChunksByDocumentAsync(1));
        Assert.Equal("Retried content", (await repo.GetChunksByDocumentAsync(1))[0].Content);
        Assert.Single(await repo.GetChunksByDocumentAsync(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task Review_page_uses_question_document_for_navigation(int suppliedDocumentId)
    {
        await using var db = await OpenAsync();
        var materials = new CourseMaterialService(new CourseMaterialRepository(db), new CatalogRepository(db), new AssignmentPRN.DataAccess.Services.LocalMaterialFileStore(Path.GetTempPath()));
        var page = new AssignmentPRN.Presentation.Pages.Documents.Questions.ReviewModel(Service(db), materials);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Features.Set<Microsoft.AspNetCore.Http.Features.ISessionFeature>(new SessionFeature { Session = new MemorySession() });
        page.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext { HttpContext = http };
        await page.OnGetAsync(1, suppliedDocumentId, CancellationToken.None);
        Assert.NotNull(page.Question);
        Assert.Equal(page.Question.DocumentId, page.DocumentId);
    }

    private sealed class SessionFeature : Microsoft.AspNetCore.Http.Features.ISessionFeature
    {
        public Microsoft.AspNetCore.Http.ISession Session { get; set; } = null!;
    }

    private sealed class MemorySession : Microsoft.AspNetCore.Http.ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }

    [Fact]
    public async Task Document_list_includes_the_uploader_display_name()
    {
        await using var db = await OpenAsync();
        var response = await Service(db).ListByCourseAsync(1);
        Assert.True(response.Success);
        Assert.Equal("Test", Assert.Single(response.Data!).UploadedByName);
    }
    private static DocumentService Service(AivesDbContext db, int id = 1, string role = "Lecturer", IHttpClientFactory? ai = null, string? root = null, IDocumentRepository? repository = null) =>
        new(repository ?? new DocumentRepository(db), new CatalogRepository(db), Options.Create(new DocumentStorageOptions { BasePath = root ?? Path.GetTempPath() }), Options.Create(new OllamaOptions()), ai ?? new NoAiCalls(), new DocumentProcessingQueue(), NullLogger<DocumentService>.Instance, new Actor(id, role), new CourseRepository(db), Options.Create(new RagOptions()));
    private sealed record Actor(int UserId, string? Role) : IDocumentActor;
    private sealed class NoAiCalls : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("This test must not call AI.");
    }
    private static async Task<AivesDbContext> OpenAsync()
    {
        var db = new AivesDbContext(new DbContextOptionsBuilder<AivesDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Roles.AddRange(new Role { RoleId = 1, RoleName = "Lecturer" }, new Role { RoleId = 2, RoleName = "Student" });
        foreach (var id in new[] { 1, 2, 3 }) db.Users.Add(new User { UserId = id, RoleId = id == 3 ? 2 : 1, FullName = "Test", Email = $"test{id}@example.test", PasswordHash = "unused", Status = "Active" });
        db.Courses.Add(new Course { CourseId = 1, LecturerId = 1, CourseCode = "TEST", CourseName = "Test", IsActive = true });
        db.Documents.Add(new Document { DocumentId = 1, CourseId = 1, UploadedById = 1, FileName = "test.docx", StoragePath = "test.docx", Status = Da.DocumentStatus.Completed });
        db.GeneratedQuestions.Add(new GeneratedQuestion { QuestionId = 1, DocumentId = 1, CourseId = 1, QuestionText = "Original", AnswerKey = "Original answer" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}
