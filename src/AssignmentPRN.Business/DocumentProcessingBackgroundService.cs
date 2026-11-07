using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Presentation;

namespace AssignmentPRN.Business;

public sealed class DocumentProcessingBackgroundService : BackgroundService
{
    private readonly DocumentProcessingQueue _processingQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DocumentProcessingBackgroundService> _logger;
    private readonly IDocumentProgressNotifier? _progress;

    public DocumentProcessingBackgroundService(
        DocumentProcessingQueue processingQueue,
        IServiceScopeFactory scopeFactory,
        IOptions<DocumentStorageOptions> storageOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentProcessingBackgroundService> logger, IDocumentProgressNotifier? progress = null)
    {
        _processingQueue = processingQueue;
        _scopeFactory = scopeFactory;
        _storageOptions = storageOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger; _progress = progress;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing background service started.");

        using (var recovery = _scopeFactory.CreateScope()) {
            var repository = recovery.ServiceProvider.GetRequiredService<IDocumentRepository>();
            foreach (var id in await repository.PendingIdsAsync(stoppingToken)) await _processingQueue.QueueDocumentAsync(id, stoppingToken);
        }
        await foreach (var documentId in _processingQueue.DequeueAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;

            using var scope = _scopeFactory.CreateScope();
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var catalogRepository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();

            await ProcessDocumentAsync(documentId, documentRepository, catalogRepository, stoppingToken);
        }
    }

    private async Task ProcessDocumentAsync(int documentId, IDocumentRepository documentRepository, ICatalogRepository catalogRepository, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing document {DocumentId}", documentId);

        try
        {
            var doc = await documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (doc is null)
            {
                _logger.LogWarning("Document {DocumentId} not found", documentId);
                return;
            }

            await documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Processing, null, cancellationToken);
            var root = Path.GetFullPath(_storageOptions.BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, doc.StoragePath));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Đường dẫn tài liệu không hợp lệ.");
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            if (_progress is not null) await _progress.ChangedAsync(new(doc.DocumentId, doc.CourseId, "Processing"), cancellationToken);
            // Extract text based on file type
            var text = ExtractText(fullPath, doc.ContentType);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Không thể trích xuất nội dung từ file.");
            }

            // Chunk text
            var chunks = ChunkText(text, _storageOptions.ChunkSize, _storageOptions.ChunkOverlap);
            _logger.LogInformation("Document {DocumentId} split into {ChunkCount} chunks", documentId, chunks.Count);

            // Generate embeddings for each chunk
            var documentChunks = new List<DocumentChunk>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var embedding = await GetEmbeddingAsync(chunks[i], cancellationToken);
                documentChunks.Add(new DocumentChunk
                {
                    DocumentId = documentId,
                    ChunkIndex = i,
                    Content = chunks[i],
                    TokenCount = EstimateTokenCount(chunks[i]),
                    EmbeddingJson = JsonSerializer.Serialize(embedding)
                });
            }

            // Save chunks
            await documentRepository.AddChunksAsync(documentChunks, cancellationToken);

            // Update document status
            await documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Completed, null, cancellationToken);

            if (_progress is not null) await _progress.ChangedAsync(new(doc.DocumentId, doc.CourseId, "Completed"), cancellationToken);
            _logger.LogInformation("Document {DocumentId} processed successfully with {ChunkCount} chunks", documentId, chunks.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Keep Processing so startup recovery can resume after shutdown.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document {DocumentId}", documentId);
            try
            {
                var message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                await documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Failed, message, cancellationToken);
                var failed = await documentRepository.GetByIdAsync(documentId, cancellationToken);
                if (_progress is not null && failed is not null) await _progress.ChangedAsync(new(documentId, failed.CourseId, "Failed"), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception statusError)
            {
                _logger.LogError(statusError, "Cannot persist failure for document {DocumentId}; leaving it for startup recovery", documentId);
            }
        }
    }

    private string ExtractText(string filePath, string contentType)
    {
        return contentType switch
        {
            "application/pdf" => ExtractPdfText(filePath),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractDocxText(filePath),
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ExtractPptxText(filePath),
            _ => throw new NotSupportedException($"Unsupported content type: {contentType}")
        };
    }

    private string ExtractPdfText(string filePath)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(filePath);

        foreach (var page in pdf.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
            }
        }

        // TODO: Add OCR fallback for scanned PDFs using Tesseract
        // if (sb.Length < 100) { /* run OCR */ }

        return sb.ToString();
    }

    private string ExtractDocxText(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document.Body;

        if (body is not null)
        {
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                var text = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
        }

        return sb.ToString();
    }

    private string ExtractPptxText(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = PresentationDocument.Open(filePath, false);
        var presentationPart = doc.PresentationPart;

        if (presentationPart is not null)
        {
            foreach (var slideId in presentationPart.Presentation.SlideIdList?.Elements<SlideId>() ?? [])
            {
                var relationId = slideId.RelationshipId?.Value;
                if (string.IsNullOrEmpty(relationId)) continue;
                var slidePart = (SlidePart)presentationPart.GetPartById(relationId);
                var slide = slidePart.Slide;

                foreach (var text in slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        sb.AppendLine(text.Text);
                    }
                }
            }
        }

        return sb.ToString();
    }

    internal static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        if (chunkSize <= 0 || overlap < 0 || overlap >= chunkSize)
            throw new ArgumentOutOfRangeException(nameof(overlap), "Chunk overlap must be non-negative and smaller than the chunk size.");
        var chunks = new List<string>();
        var words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0) return chunks;

        int start = 0;
        while (start < words.Length)
        {
            int end = Math.Min(start + chunkSize, words.Length);
            var chunk = string.Join(' ', words[start..end]);
            chunks.Add(chunk);

            if (end >= words.Length) break;
            start = end - overlap;
            if (start < 0) start = 0;
        }

        return chunks;
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 chars for Vietnamese/English
        return Math.Max(1, text.Length / 4);
    }

    private async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_ollamaOptions.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(2);

        var request = new
        {
            model = _ollamaOptions.EmbeddingModel,
            prompt = text
        };

        var response = await client.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
        return result?.Embedding is { Length: > 0 } vector ? vector : throw new InvalidOperationException("Embedding model returned no vector.");
    }

    private sealed record OllamaEmbeddingResponse(float[] Embedding);
}