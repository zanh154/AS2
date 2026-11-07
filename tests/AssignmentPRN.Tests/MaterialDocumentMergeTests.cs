using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Da = AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.Tests;

/// <summary>
/// A course material and its AI index (document, chunks, generated questions) are one
/// item for the lecturer: listed together, moved together, deleted together.
/// </summary>
public partial class DocumentOperationalTests
{
    [Fact]
    public async Task Material_list_shows_the_ai_index_of_its_file()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);

        var item = Assert.Single(await new CourseMaterialRepository(db).ListAsync(1));

        Assert.Equal(1, item.DocumentId);
        Assert.Equal(Da.DocumentStatus.Completed, item.AiStatus);
        Assert.Equal(1, item.GeneratedQuestionCount);
        Assert.Equal(item.QuestionCount, item.ManualQuestionCount + item.AiQuestionCount);
        Assert.Equal(1, item.PendingGeneratedQuestionCount);
        Assert.Equal(0, item.ApprovedGeneratedQuestionCount);
        Assert.Equal(0, item.RejectedGeneratedQuestionCount);
    }

    [Fact]
    public async Task A_material_without_an_index_lists_no_ai_status()
    {
        await using var db = await OpenAsync();
        db.CourseMaterials.Add(Material(7, 1));
        await db.SaveChangesAsync();

        var item = Assert.Single(await new CourseMaterialRepository(db).ListAsync(1));

        Assert.Null(item.DocumentId);
        Assert.Null(item.AiStatus);
        Assert.Equal(0, item.GeneratedQuestionCount);
        Assert.Equal(0, item.PendingGeneratedQuestionCount);
        Assert.Equal(0, item.ApprovedGeneratedQuestionCount);
        Assert.Equal(0, item.RejectedGeneratedQuestionCount);
    }

    [Fact]
    public async Task Material_list_breaks_generated_questions_down_by_review_status()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);
        db.GeneratedQuestions.AddRange(
            new GeneratedQuestion { QuestionId = 2, DocumentId = 1, CourseId = 1, QuestionText = "Approved", Status = Da.GeneratedQuestionStatus.Approved },
            new GeneratedQuestion { QuestionId = 3, DocumentId = 1, CourseId = 1, QuestionText = "Rejected", Status = Da.GeneratedQuestionStatus.Rejected });
        await db.SaveChangesAsync();

        var item = Assert.Single(await new CourseMaterialRepository(db).ListAsync(1));

        Assert.Equal(3, item.GeneratedQuestionCount);
        Assert.Equal(1, item.PendingGeneratedQuestionCount);
        Assert.Equal(1, item.ApprovedGeneratedQuestionCount);
        Assert.Equal(1, item.RejectedGeneratedQuestionCount);
    }

    [Fact]
    public async Task Deleting_a_material_drops_its_ai_index()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);
        db.DocumentChunks.Add(new DocumentChunk { DocumentId = 1, ChunkIndex = 0, Content = "chunk", TokenCount = 1, EmbeddingJson = "[1]" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await new CourseMaterialRepository(db).DeleteAsync(7);
        db.ChangeTracker.Clear();

        Assert.Empty(await db.Documents.ToListAsync());
        Assert.Empty(await db.DocumentChunks.ToListAsync());
        Assert.Empty(await db.GeneratedQuestions.ToListAsync());
    }

    [Fact]
    public async Task Moving_a_material_moves_its_ai_index_to_the_new_course()
    {
        await using var db = await OpenAsync();
        db.Courses.Add(new Course { CourseId = 2, LecturerId = 1, CourseCode = "OTHER", CourseName = "Other", IsActive = true });
        await LinkDocumentToMaterialAsync(db, materialId: 7);
        db.ChangeTracker.Clear();

        await new CourseMaterialRepository(db).UpdateAsync(7, 2, "renamed.pdf");
        db.ChangeTracker.Clear();

        var document = await db.Documents.SingleAsync();
        Assert.Equal(2, document.CourseId);
        Assert.Equal("renamed.pdf", document.FileName);
        Assert.Equal(2, (await db.GeneratedQuestions.SingleAsync()).CourseId);
    }

    [Fact]
    public async Task A_material_has_at_most_one_ai_index()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);
        // Untracked, so the unique index in the database has to refuse it, not EF fix-up.
        db.ChangeTracker.Clear();
        db.Documents.Add(new Document { DocumentId = 2, CourseId = 1, UploadedById = 1, FileName = "copy.pdf", StoragePath = "copy.pdf", MaterialId = 7 });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>Files the seeded document 1 (with its one generated question) under a new material.</summary>
    private static async Task LinkDocumentToMaterialAsync(AssignmentPRN.DataAccess.Data.AivesDbContext db, int materialId)
    {
        db.CourseMaterials.Add(Material(materialId, 1));
        await db.SaveChangesAsync();
        (await db.Documents.SingleAsync(x => x.DocumentId == 1)).MaterialId = materialId;
        await db.SaveChangesAsync();
    }
}

public partial class DocumentOperationalTests
{
    [Fact]
    public async Task Uploading_a_material_queues_its_ai_index()
    {
        await using var db = await OpenAsync();
        var folder = TestFolder();
        try
        {
            var queue = new AssignmentPRN.Business.DocumentProcessingQueue();
            var result = await MaterialService(db, folder, new DocumentRepository(db), queue)
                .UploadAsync(1, "chuong-1.pdf", new MemoryStream([1, 2, 3]), 3, AssignmentPRN.Business.Interfaces.MaterialFileType.PDF, 1);

            Assert.True(result.Success, result.Error);
            Assert.Equal(AssignmentPRN.Business.Interfaces.DocumentStatus.Pending, result.Data!.AiStatus);
            db.ChangeTracker.Clear();
            var document = await db.Documents.SingleAsync(x => x.MaterialId == result.Data.MaterialId);
            Assert.Equal(result.Data.DocumentId, document.DocumentId);
            Assert.Equal("application/pdf", document.ContentType);
            Assert.Equal(result.Data.FilePath, document.StoragePath);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var pending = queue.DequeueAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
            Assert.True(await pending.MoveNextAsync());
            Assert.Equal(document.DocumentId, pending.Current);
        }
        finally { DeleteTestFolder(folder); }
    }

    [Fact]
    public async Task A_failed_index_undoes_the_whole_upload()
    {
        await using var db = await OpenAsync();
        var folder = TestFolder();
        try
        {
            var repo = System.Reflection.DispatchProxy.Create<IDocumentRepository, RepositoryFault>();
            ((RepositoryFault)repo).Inner = new DocumentRepository(db);
            ((RepositoryFault)repo).FailMethod = nameof(IDocumentRepository.CreateAsync);

            var result = await MaterialService(db, folder, repo, new AssignmentPRN.Business.DocumentProcessingQueue())
                .UploadAsync(1, "chuong-1.pdf", new MemoryStream([1, 2, 3]), 3, AssignmentPRN.Business.Interfaces.MaterialFileType.PDF, 1);

            Assert.False(result.Success);
            db.ChangeTracker.Clear();
            Assert.Empty(await db.CourseMaterials.ToListAsync());
            Assert.Single(await db.Documents.ToListAsync());
            Assert.Empty(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
        }
        finally { DeleteTestFolder(folder); }
    }

    [Fact]
    public async Task A_rejected_material_leaves_no_file_and_no_index()
    {
        await using var db = await OpenAsync();
        db.CourseMaterials.Add(Material(7, 1));
        await db.SaveChangesAsync();
        var folder = TestFolder();
        try
        {
            var result = await MaterialService(db, folder, new DocumentRepository(db), new AssignmentPRN.Business.DocumentProcessingQueue())
                .UploadAsync(1, "topic-7.pdf", new MemoryStream([1, 2, 3]), 3, AssignmentPRN.Business.Interfaces.MaterialFileType.PDF, 1);

            Assert.False(result.Success);
            Assert.Single(await db.Documents.ToListAsync());
            Assert.Empty(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
        }
        finally { DeleteTestFolder(folder); }
    }

    [Fact]
    public async Task Approval_files_a_material_question_under_that_material_by_default()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);

        var result = await Service(db).ReviewQuestionAsync(1, 1, new(AssignmentPRN.Business.Interfaces.GeneratedQuestionStatus.Approved, null));

        Assert.True(result.Success, result.Error);
        db.ChangeTracker.Clear();
        Assert.Equal(7, (await db.Questions.SingleAsync()).SourceMaterialId);
    }

    [Fact]
    public async Task A_material_index_cannot_be_deleted_on_its_own()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);

        var result = await Service(db).DeleteAsync(1, 1);

        Assert.False(result.Success);
        Assert.Single(await db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Indexing_a_material_from_before_the_merge_creates_and_queues_its_index()
    {
        await using var db = await OpenAsync();
        db.CourseMaterials.Add(Material(7, 1));
        await db.SaveChangesAsync();
        var queue = new AssignmentPRN.Business.DocumentProcessingQueue();

        var result = await MaterialService(db, Path.GetTempPath(), new DocumentRepository(db), queue).IndexAsync(7, lecturerId: 1, requestedBy: 1);

        Assert.True(result.Success, result.Error);
        db.ChangeTracker.Clear();
        var document = await db.Documents.SingleAsync(x => x.MaterialId == 7);
        Assert.Equal(Da.DocumentStatus.Pending, document.Status);
        Assert.Equal("/materials/7.pdf".TrimStart('/'), document.StoragePath.TrimStart('/'));
        Assert.Equal(document.DocumentId, await FirstQueuedAsync(queue));
    }

    [Fact]
    public async Task Indexing_a_failed_material_queues_it_again()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);
        (await db.Documents.SingleAsync()).Status = Da.DocumentStatus.Failed;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var queue = new AssignmentPRN.Business.DocumentProcessingQueue();

        var result = await MaterialService(db, Path.GetTempPath(), new DocumentRepository(db), queue).IndexAsync(7, lecturerId: 1, requestedBy: 1);

        Assert.True(result.Success, result.Error);
        db.ChangeTracker.Clear();
        Assert.Equal(Da.DocumentStatus.Pending, (await db.Documents.SingleAsync()).Status);
        Assert.Equal(1, await FirstQueuedAsync(queue));
    }

    [Fact]
    public async Task Indexing_a_material_that_is_already_indexed_is_refused()
    {
        await using var db = await OpenAsync();
        await LinkDocumentToMaterialAsync(db, materialId: 7);

        var result = await MaterialService(db, Path.GetTempPath(), new DocumentRepository(db), new AssignmentPRN.Business.DocumentProcessingQueue()).IndexAsync(7, lecturerId: 1, requestedBy: 1);

        Assert.False(result.Success);
        Assert.Single(await db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Indexing_another_lecturers_material_is_refused()
    {
        await using var db = await OpenAsync();
        db.CourseMaterials.Add(Material(7, 1));
        await db.SaveChangesAsync();

        var result = await MaterialService(db, Path.GetTempPath(), new DocumentRepository(db), new AssignmentPRN.Business.DocumentProcessingQueue()).IndexAsync(7, lecturerId: 2, requestedBy: 2);

        Assert.False(result.Success);
        Assert.DoesNotContain(await db.Documents.ToListAsync(), x => x.MaterialId == 7);
    }

    [Fact]
    public async Task Worker_reads_a_material_file_through_the_material_store()
    {
        await using var db = await OpenAsync();
        var store = TestFolder();
        var legacyRoot = TestFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(store, "materials"));
            using (var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(Path.Combine(store, "materials", "lesson.docx"), DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var part = word.AddMainDocumentPart();
                part.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(new DocumentFormat.OpenXml.Wordprocessing.Body(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Encapsulation hides state behind methods.")))));
                part.Document.Save();
            }
            var material = Material(7, 1);
            material.FilePath = "/materials/lesson.docx";
            db.CourseMaterials.Add(material);
            db.Documents.Add(new Document { DocumentId = 2, CourseId = 1, UploadedById = 1, MaterialId = 7, FileName = "lesson.docx", StoragePath = "/materials/lesson.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Status = Da.DocumentStatus.Pending });
            await db.SaveChangesAsync();

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IDocumentRepository>(services, new DocumentRepository(db));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ICatalogRepository>(services, new CatalogRepository(db));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<AssignmentPRN.DataAccess.Services.IMaterialFileStore>(services, new AssignmentPRN.DataAccess.Services.LocalMaterialFileStore(store));
            using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            var progress = new DocProgress();
            using var worker = new AssignmentPRN.Business.DocumentProcessingBackgroundService(
                new AssignmentPRN.Business.DocumentProcessingQueue(),
                Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(provider),
                Microsoft.Extensions.Options.Options.Create(new AssignmentPRN.Business.DocumentStorageOptions { BasePath = legacyRoot }),
                Microsoft.Extensions.Options.Options.Create(new AssignmentPRN.Business.OllamaOptions()),
                new FixtureAi(Questions(1)),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AssignmentPRN.Business.DocumentProcessingBackgroundService>.Instance,
                progress);
            try
            {
                await worker.StartAsync(CancellationToken.None);
                Assert.Equal(2, (await progress.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10))).DocumentId);
            }
            finally { await worker.StopAsync(CancellationToken.None); }

            db.ChangeTracker.Clear();
            Assert.Equal(Da.DocumentStatus.Completed, (await db.Documents.SingleAsync(x => x.DocumentId == 2)).Status);
            Assert.Contains(await db.DocumentChunks.Where(x => x.DocumentId == 2).ToListAsync(), x => x.Content.Contains("Encapsulation"));
        }
        finally { DeleteTestFolder(store); DeleteTestFolder(legacyRoot); }
    }

    private static async Task<int> FirstQueuedAsync(AssignmentPRN.Business.DocumentProcessingQueue queue)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var pending = queue.DequeueAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.True(await pending.MoveNextAsync());
        return pending.Current;
    }

    private static AssignmentPRN.Business.Services.CourseMaterialService MaterialService(
        AssignmentPRN.DataAccess.Data.AivesDbContext db,
        string folder,
        IDocumentRepository documents,
        AssignmentPRN.Business.DocumentProcessingQueue queue) =>
        new(new CourseMaterialRepository(db), new CatalogRepository(db),
            new AssignmentPRN.DataAccess.Services.LocalMaterialFileStore(folder), documents, queue);
}
