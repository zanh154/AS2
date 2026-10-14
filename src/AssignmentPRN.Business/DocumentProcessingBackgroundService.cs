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

    public DocumentProcessingBackgroundService(
        DocumentProcessingQueue processingQueue,
        IServiceScopeFactory scopeFactory,
        IOptions<DocumentStorageOptions> storageOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<DocumentProcessingBackgroundService> logger)
    {
        _processingQueue = processingQueue;
        _scopeFactory = scopeFactory;
        _storageOptions = storageOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing background service started.");

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

        await documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Processing, null, cancellationToken);

        try
        {
            var doc = await documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (doc is null)
            {
                _logger.LogWarning("Document {DocumentId} not found", documentId);
                return;
            }

            var fullPath = Path.Combine(_storageOptions.BasePath, doc.StoragePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

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

            _logger.LogInformation("Document {DocumentId} processed successfully with {ChunkCount} chunks", documentId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document {DocumentId}", documentId);
            await documentRepository.UpdateStatusAsync(documentId, DocumentStatus.Failed, ex.Message, cancellationToken);
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
                var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId);
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

    private List<string> ChunkText(string text, int chunkSize, int overlap)
    {
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
        return result?.Embedding ?? [];
    }

    private sealed record OllamaEmbeddingResponse(float[] Embedding);
}