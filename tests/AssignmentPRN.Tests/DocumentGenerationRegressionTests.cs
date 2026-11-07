using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Tests;

public partial class DocumentOperationalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public async Task Valid_AI_batch_saves_exact_count_bloom_and_requested_topic(int count)
    {
        await using var db = await WithChunksAsync();
        var ai = new FixtureAi(Questions(count));
        var result = await Service(db, ai: ai).GenerateQuestionsAsync(1, new(count, DocumentBloomLevel.Understand, "C#"));
        Assert.True(result.Success, result.Error);
        Assert.Equal(count, result.Data!.Count);
        Assert.All(result.Data, q => { Assert.Equal("C#", q.Topic); Assert.Equal(DocumentBloomLevel.Understand, q.BloomLevel); Assert.Equal(1, q.DocumentId); });
        Assert.Equal(count + 1, await db.GeneratedQuestions.CountAsync());
        Assert.Equal((count, count), Assert.Single(ai.SchemaCounts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task AI_wrong_count_never_saves_a_partial_result(int returned)
    {
        await using var db = await WithChunksAsync();
        var result = await Service(db, ai: new FixtureAi(Questions(returned))).GenerateQuestionsAsync(1, new(2, DocumentBloomLevel.Understand, null));
        Assert.False(result.Success);
        Assert.Contains("Chưa lưu câu nào", result.Error);
        Assert.Equal(1, await db.GeneratedQuestions.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("questionText", "")]
    [InlineData("answerKey", "")]
    [InlineData("answerKey", "long")]
    [InlineData("explanation", "long")]
    [InlineData("topic", "long")]
    public async Task Invalid_second_AI_question_is_rejected_before_saving_first(string field, string value)
    {
        await using var db = await WithChunksAsync();
        var json = JsonNode.Parse(Questions(2))!;
        json["questions"]![1]![field] = value == "long" ? new string('x', field == "topic" ? 201 : 2001) : value;
        var result = await Service(db, ai: new FixtureAi(json.ToJsonString())).GenerateQuestionsAsync(1, new(2, DocumentBloomLevel.Understand, null));
        Assert.False(result.Success);
        Assert.Equal(1, await db.GeneratedQuestions.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task AI_wrong_or_unknown_bloom_is_not_silently_accepted(int bloom)
    {
        await using var db = await WithChunksAsync();
        var json = JsonNode.Parse(Questions(2))!; json["questions"]![1]!["bloomLevel"] = bloom;
        var result = await Service(db, ai: new FixtureAi(json.ToJsonString())).GenerateQuestionsAsync(1, new(2, DocumentBloomLevel.Understand, null));
        Assert.False(result.Success);
        Assert.Equal(1, await db.GeneratedQuestions.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"questions\":null}")]
    [InlineData("{\"questions\":[null,null]}")]
    public async Task Malformed_AI_response_does_not_persist_questions(string raw)
    {
        await using var db = await WithChunksAsync();
        Assert.False((await Service(db, ai: new FixtureAi(raw)).GenerateQuestionsAsync(1, new(2, null, null))).Success);
        Assert.Equal(1, await db.GeneratedQuestions.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Duplicate_AI_questions_are_not_saved()
    {
        await using var db = await WithChunksAsync();
        var json = JsonNode.Parse(Questions(2))!;
        json["questions"]![1]!["questionText"] = " question 0 ";
        Assert.False((await Service(db, ai: new FixtureAi(json.ToJsonString())).GenerateQuestionsAsync(1, new(2, null, null))).Success);
        Assert.Equal(1, await db.GeneratedQuestions.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Database_failure_during_batch_rolls_back_all_new_questions()
    {
        await using var db = await WithChunksAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_ai BEFORE INSERT ON generated_questions WHEN NEW.question_text='REJECT_BATCH' BEGIN SELECT RAISE(ABORT, 'simulated batch failure'); END;");
        var json = JsonNode.Parse(Questions(2))!; json["questions"]![1]!["questionText"] = "REJECT_BATCH";
        Assert.False((await Service(db, ai: new FixtureAi(json.ToJsonString())).GenerateQuestionsAsync(1, new(2, null, null))).Success);
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.GeneratedQuestions.CountAsync());
    }

    [Fact]
    public async Task Former_uploader_cannot_list_documents_of_a_reassigned_course()
    {
        await using var db = await OpenAsync();
        (await db.Courses.SingleAsync()).LecturerId = 2; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var response = await Service(db).ListByUserAsync(1);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
    }

    [Fact]
    public async Task Lists_load_course_uploader_and_reviewer_names()
    {
        await using var db = await OpenAsync();
        var svc = Service(db);
        Assert.True((await svc.ReviewQuestionAsync(1, 1, new(GeneratedQuestionStatus.Approved, "checked"))).Success);
        db.ChangeTracker.Clear();
        Assert.Equal("Test", Assert.Single((await svc.ListByUserAsync(1)).Data!).UploadedByName);
        Assert.Equal("TEST", Assert.Single((await svc.ListByUserAsync(1)).Data!).CourseCode);
        db.ChangeTracker.Clear();
        Assert.Equal("Test", Assert.Single((await svc.ListQuestionsByDocumentAsync(1)).Data!).ReviewedByName);
        db.ChangeTracker.Clear();
        Assert.Equal("Test", Assert.Single((await svc.ListQuestionsByCourseAsync(1)).Data!).ReviewedByName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Failed_upload_leaves_no_orphan_file(bool failDatabase)
    {
        await using var db = await OpenAsync();
        var folder = TestFolder();
        try {
            var repo = DispatchProxy.Create<IDocumentRepository, RepositoryFault>();
            ((RepositoryFault)repo).Inner = new DocumentRepository(db);
            ((RepositoryFault)repo).FailMethod = failDatabase ? nameof(IDocumentRepository.CreateAsync) : "never";
            IFormFile file = failDatabase ? new FormFile(new MemoryStream([1, 2]), 0, 2, "file", "test.pdf") : new BrokenUpload();
            Assert.False((await Service(db, root: folder, repository: repo).UploadAsync(1, 1, file)).Success);
            Assert.Empty(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
            Assert.Equal(1, await db.Documents.CountAsync());
        } finally { DeleteTestFolder(folder); }
    }

    [Fact]
    public async Task Database_delete_failure_preserves_existing_file()
    {
        await using var db = await OpenAsync(); var folder = TestFolder();
        try {
            var file = Path.Combine(folder, "test.docx"); await File.WriteAllTextAsync(file, "keep");
            var repo = DispatchProxy.Create<IDocumentRepository, RepositoryFault>();
            ((RepositoryFault)repo).Inner = new DocumentRepository(db); ((RepositoryFault)repo).FailMethod = nameof(IDocumentRepository.DeleteDocumentAsync);
            Assert.False((await Service(db, root: folder, repository: repo).DeleteAsync(1, 1)).Success);
            Assert.True(File.Exists(file)); Assert.Single(await db.Documents.ToListAsync());
        } finally { DeleteTestFolder(folder); }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, -1)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public void Invalid_chunk_settings_fail_without_entering_an_infinite_loop(int size, int overlap) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentProcessingBackgroundService.ChunkText("one two three", size, overlap));

    [Fact]
    public void Chunk_overlap_preserves_tail_and_ends_after_last_word() =>
        Assert.Equal(new[] { "one two", "two three" }, DocumentProcessingBackgroundService.ChunkText("one two three", 2, 1));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public void Invalid_chunk_configuration_is_rejected_at_startup(int size, int overlap)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["DocumentStorage:ChunkSize"] = size.ToString(), ["DocumentStorage:ChunkOverlap"] = overlap.ToString()
        }).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config);
        services.AddBusiness("Server=unused;Database=unused;User=unused;Password=unused", Path.GetTempPath());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DocumentStorageOptions>>().Value);
    }

    [Fact]
    public async Task AI_unavailable_does_not_save_questions()
    {
        await using var db = await WithChunksAsync();
        Assert.False((await Service(db, ai: new FixtureAi("HTTP503")).GenerateQuestionsAsync(1, new(2, null, null))).Success);
        Assert.Equal(1, await db.GeneratedQuestions.CountAsync());
    }

    [Fact]
    public async Task Worker_continues_to_next_document_when_failure_status_cannot_be_saved()
    {
        await using var db = await OpenAsync(); var folder = TestFolder();
        try {
            (await db.Documents.SingleAsync()).Status = DocumentStatus.Pending;
            db.Documents.Add(new Document { DocumentId = 2, CourseId = 1, UploadedById = 1, FileName = "next.docx", StoragePath = "next.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Status = DocumentStatus.Pending });
            await db.SaveChangesAsync();
            using (var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(Path.Combine(folder, "next.docx"), DocumentFormat.OpenXml.WordprocessingDocumentType.Document)) {
                var part = word.AddMainDocumentPart();
                part.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new DocumentFormat.OpenXml.Wordprocessing.Body(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Object oriented programming uses classes and objects.")))));
                part.Document.Save();
            }
            var repo = DispatchProxy.Create<IDocumentRepository, RepositoryFault>(); var fault = (RepositoryFault)repo;
            fault.Inner = new DocumentRepository(db); fault.FailMethod = nameof(IDocumentRepository.UpdateStatusAsync); fault.FailId = 1;
            var services = new ServiceCollection(); services.AddSingleton(repo); services.AddSingleton<ICatalogRepository>(new CatalogRepository(db));
            using var provider = services.BuildServiceProvider(); var progress = new DocProgress();
            using var worker = new DocumentProcessingBackgroundService(new DocumentProcessingQueue(), provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new DocumentStorageOptions { BasePath = folder }), Options.Create(new OllamaOptions()), new FixtureAi(Questions(1)), Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentProcessingBackgroundService>.Instance, progress);
            try {
                await worker.StartAsync(CancellationToken.None);
                var completed = await progress.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(2, completed.DocumentId);
                Assert.True(fault.Failures > 0);
            } finally { await worker.StopAsync(CancellationToken.None); }
            Assert.False(worker.ExecuteTask?.IsFaulted ?? false);
            db.ChangeTracker.Clear(); Assert.Equal(DocumentStatus.Completed, (await db.Documents.SingleAsync(x => x.DocumentId == 2)).Status);
        } finally { DeleteTestFolder(folder); }
    }

    private sealed class DocProgress : IDocumentProgressNotifier
    {
        public TaskCompletionSource<DocumentProgress> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ChangedAsync(DocumentProgress progress, CancellationToken ct = default) { if (progress.Status == "Completed") Completed.TrySetResult(progress); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Oversized_upload_is_rejected_before_writing_any_file()
    {
        await using var db = await OpenAsync();
        var file = new FormFile(Stream.Null, 0, 51L * 1024 * 1024, "file", "large.pdf");
        var response = await Service(db).UploadAsync(1, 1, file);
        Assert.False(response.Success);
        Assert.Contains("File quá lớn", response.Error);
        Assert.Single(await db.Documents.ToListAsync());
    }

    private static async Task<AivesDbContext> WithChunksAsync()
    {
        var db = await OpenAsync();
        db.DocumentChunks.Add(new DocumentChunk { DocumentId = 1, ChunkIndex = 0, Content = "Object oriented programming uses classes and objects.", EmbeddingJson = "[1,0]" });
        await db.SaveChangesAsync(); return db;
    }
    private static string Questions(int count) => JsonSerializer.Serialize(new { questions = Enumerable.Range(0,count).Select(i => new {
        questionText = "Question " + i, answerKey = "Answer " + i, explanation = "Reason", bloomLevel = 2, topic = "OOP"
    }) });
    private static string TestFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "aives-task3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); return folder;
    }
    private static void DeleteTestFolder(string folder)
    {
        var full = Path.GetFullPath(folder);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), full, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("aives-task3-", Path.GetFileName(full));
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
    private sealed class FixtureAi(string raw) : IHttpClientFactory
    {
        public List<(int Min,int Max)> SchemaCounts { get; } = [];
        public HttpClient CreateClient(string name) => new(new FixtureHandler(raw, SchemaCounts));
    }
    private sealed class FixtureHandler(string raw, List<(int,int)> schemaCounts) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (raw == "HTTP503") return new(HttpStatusCode.ServiceUnavailable);
            if (request.RequestUri!.AbsolutePath.EndsWith("embeddings")) return new(HttpStatusCode.OK) { Content = new StringContent("{\"embedding\":[1,0]}", Encoding.UTF8, "application/json") };
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var schema = body.RootElement.GetProperty("format").GetProperty("properties").GetProperty("questions");
            schemaCounts.Add((schema.GetProperty("minItems").GetInt32(), schema.GetProperty("maxItems").GetInt32()));
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { response = raw }), Encoding.UTF8, "application/json") };
        }
    }
    public class RepositoryFault : DispatchProxy
    {
        public IDocumentRepository Inner { get; set; } = null!;
        public string FailMethod { get; set; } = "";
        public int? FailId { get; set; }
        public int Failures { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == FailMethod && (!FailId.HasValue || Convert.ToInt32(args![0]) == FailId.Value)) { Failures++; throw new IOException("Simulated database error"); }
            return method.Invoke(Inner, args);
        }
    }
    private sealed class BrokenUpload : IFormFile
    {
        public string ContentType => "application/pdf";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 2;
        public string Name => "file";
        public string FileName => "test.pdf";
        public Stream OpenReadStream() => throw new IOException("Broken stream");
        public void CopyTo(Stream target) => throw new IOException("Broken stream");
        public async Task CopyToAsync(Stream target, CancellationToken ct = default) { await target.WriteAsync(new byte[] { 1 }, ct); throw new IOException("Broken stream"); }
    }
}
