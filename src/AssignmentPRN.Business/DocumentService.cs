using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AssignmentPRN.Business;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DocumentProcessingQueue _processingQueue;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        ICatalogRepository catalogRepository,
        IOptions<DocumentStorageOptions> storageOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IHttpClientFactory httpClientFactory,
        DocumentProcessingQueue processingQueue,
        ILogger<DocumentService> logger)
    {
        _documentRepository = documentRepository;
        _catalogRepository = catalogRepository;
        _storageOptions = storageOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _httpClientFactory = httpClientFactory;
        _processingQueue = processingQueue;
        _logger = logger;
    }

    public async Task<ServiceResponse<DocumentResponse>> UploadAsync(int courseId, int userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            // Validate course exists and user has access
            var courseExists = await _catalogRepository.CourseExistsAsync(courseId, cancellationToken);
            if (!courseExists)
            {
                throw new BusinessValidationException("Không tìm thấy môn học.");
            }

            // Validate file
            ValidateFile(file);

            // Generate unique filename and save
            var fileId = Guid.NewGuid().ToString("N")[..12];
            var extension = Path.GetExtension(file.FileName);
            var storedFileName = $"{fileId}{extension}";
            var relativePath = Path.Combine("documents", courseId.ToString(), storedFileName);
            var fullPath = Path.Combine(_storageOptions.BasePath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            // Create document record
            var document = new Document
            {
                CourseId = courseId,
                UploadedById = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                StoragePath = relativePath,
                Status = DocumentStatus.Pending
            };

            var created = await _documentRepository.CreateAsync(document, cancellationToken);

            // Queue for background processing
            await _processingQueue.QueueDocumentAsync(created.DocumentId, cancellationToken);

            return MapToResponse(created, 0, 0);
        }, "Không thể tải lên tài liệu.");
    }

    public async Task<ServiceResponse<DocumentResponse>> GetAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");

            var chunks = await _documentRepository.GetChunksByDocumentAsync(documentId, cancellationToken);
            var questions = await _documentRepository.GetQuestionsByDocumentAsync(documentId, cancellationToken);

            return MapToResponse(doc, chunks.Count, questions.Count);
        }, "Không thể tải tài liệu.");
    }

    public async Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<DocumentResponse>>(async () =>
        {
            var docs = await _documentRepository.ListByCourseAsync(courseId, cancellationToken);
            var results = new List<DocumentResponse>();

            foreach (var doc in docs)
            {
                var chunks = await _documentRepository.GetChunksByDocumentAsync(doc.DocumentId, cancellationToken);
                var questions = await _documentRepository.GetQuestionsByDocumentAsync(doc.DocumentId, cancellationToken);
                results.Add(MapToResponse(doc, chunks.Count, questions.Count));
            }

            return results;
        }, "Không thể tải danh sách tài liệu.");
    }

    public async Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<DocumentResponse>>(async () =>
        {
            var docs = await _documentRepository.ListByUserAsync(userId, cancellationToken);
            var results = new List<DocumentResponse>();

            foreach (var doc in docs)
            {
                var chunks = await _documentRepository.GetChunksByDocumentAsync(doc.DocumentId, cancellationToken);
                var questions = await _documentRepository.GetQuestionsByDocumentAsync(doc.DocumentId, cancellationToken);
                results.Add(MapToResponse(doc, chunks.Count, questions.Count));
            }

            return results;
        }, "Không thể tải danh sách tài liệu.");
    }

    public async Task<ServiceResponse> DeleteAsync(int documentId, int userId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");

            if (doc.UploadedById != userId)
            {
                // Check if user is admin/lecturer of the course
                var courseExists = await _catalogRepository.CourseExistsAsync(doc.CourseId, cancellationToken);
                if (!courseExists)
                {
                    throw new BusinessValidationException("Không có quyền xoá tài liệu này.");
                }
            }

            // Delete physical file
            var fullPath = Path.Combine(_storageOptions.BasePath, doc.StoragePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            await _documentRepository.DeleteDocumentAsync(documentId, cancellationToken);
        }, "Không thể xoá tài liệu.");
    }

    public async Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> GenerateQuestionsAsync(int documentId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");

            if (doc.Status != DocumentStatus.Completed)
            {
                throw new BusinessValidationException("Tài liệu chưa được xử lý xong. Vui lòng đợi xử lý hoàn tất.");
            }

            // Get relevant chunks for context (use first few chunks as context)
            var chunks = await _documentRepository.GetChunksByDocumentAsync(documentId, cancellationToken);
            var contextChunks = chunks.Take(10).Select(c => c.Content).ToList();
            var context = string.Join("\n\n---\n\n", contextChunks);

            // Build prompt
            var prompt = BuildGenerationPrompt(context, request);

            // Call Ollama
            var questionsJson = await CallOllamaGenerateAsync(prompt, cancellationToken);

            // Parse and save questions
            var questions = ParseQuestionsResponse(questionsJson, documentId, doc.CourseId, request);
            var savedQuestions = new List<GeneratedQuestionResponse>();

            foreach (var q in questions)
            {
                var saved = await _documentRepository.CreateQuestionAsync(q, cancellationToken);
                savedQuestions.Add(MapQuestionToResponse(saved));
            }

            return (IReadOnlyList<GeneratedQuestionResponse>)savedQuestions;
        }, "Không thể sinh câu hỏi.");
    }

    public async Task<ServiceResponse<GeneratedQuestionResponse>> GetQuestionAsync(int questionId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var question = await _documentRepository.GetQuestionByIdAsync(questionId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

            return MapQuestionToResponse(question);
        }, "Không thể tải câu hỏi.");
    }

    public async Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<GeneratedQuestionResponse>>(async () =>
        {
            var questions = await _documentRepository.GetQuestionsByDocumentAsync(documentId, cancellationToken);
            return questions.Select(MapQuestionToResponse).ToList();
        }, "Không thể tải danh sách câu hỏi.");
    }

    public async Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByCourseAsync(int courseId, QuestionStatus? status = null, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<GeneratedQuestionResponse>>(async () =>
        {
            var questions = await _documentRepository.GetQuestionsByCourseAsync(courseId, status, cancellationToken);
            return questions.Select(MapQuestionToResponse).ToList();
        }, "Không thể tải danh sách câu hỏi.");
    }

    public async Task<ServiceResponse> ReviewQuestionAsync(int questionId, int reviewerId, ReviewQuestionRequest request, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var question = await _documentRepository.GetQuestionByIdAsync(questionId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");

            await _documentRepository.UpdateQuestionStatusAsync(questionId, request.Status, reviewerId, request.Notes, cancellationToken);
        }, "Không thể duyệt câu hỏi.");
    }

    private void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new BusinessValidationException("Vui lòng chọn file.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_storageOptions.AllowedExtensions.Contains(extension))
        {
            throw new BusinessValidationException($"Định dạng file không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", _storageOptions.AllowedExtensions)}");
        }

        var maxSizeBytes = _storageOptions.MaxFileSizeMB * 1024L * 1024L;
        if (file.Length > maxSizeBytes)
        {
            throw new BusinessValidationException($"File quá lớn. Kích thước tối đa: {_storageOptions.MaxFileSizeMB} MB.");
        }
    }

    private static DocumentResponse MapToResponse(Document doc, int chunkCount, int questionCount) => new(
        doc.DocumentId,
        doc.CourseId,
        doc.Course?.CourseCode ?? string.Empty,
        doc.Course?.CourseName ?? string.Empty,
        doc.UploadedById,
        doc.UploadedBy?.FullName ?? string.Empty,
        doc.FileName,
        doc.ContentType,
        doc.FileSize,
        doc.Status,
        doc.ErrorMessage,
        doc.CreatedAt,
        doc.ProcessedAt,
        chunkCount,
        questionCount
    );

    private static GeneratedQuestionResponse MapQuestionToResponse(GeneratedQuestion q) => new(
        q.QuestionId,
        q.DocumentId,
        q.CourseId,
        q.QuestionText,
        q.AnswerKey,
        q.Explanation,
        q.BloomLevel,
        q.Topic,
        q.Status,
        q.ReviewedById,
        q.ReviewedBy?.FullName,
        q.ReviewedAt,
        q.ReviewNotes,
        q.CreatedAt
    );

    private static string BuildGenerationPrompt(string context, GenerateQuestionsRequest request)
    {
        var bloomDesc = request.BloomLevel switch
        {
            BloomLevel.Remember => "Nhớ (Remember): Nhận biết, nhớ lại thông tin",
            BloomLevel.Understand => "Hiểu (Understand): Giải thích, tóm tắt, diễn giải",
            BloomLevel.Apply => "Áp dụng (Apply): Sử dụng kiến thức vào tình huống mới",
            BloomLevel.Analyze => "Phân tích (Analyze): Phân tách, so sánh, đối chiếu",
            BloomLevel.Evaluate => "Đánh giá (Evaluate): Phán xét, critique, bảo vệ quan điểm",
            BloomLevel.Create => "Sáng tạo (Create): Thiết kế, xây dựng, sáng tạo mới",
            _ => "Tự do"
        };

        var topicPart = string.IsNullOrWhiteSpace(request.Topic) ? "" : $"\nChủ đề tập trung: {request.Topic}";
        var langPart = request.Language == "en" ? "Write questions in English." : "Viết câu hỏi bằng tiếng Việt.";

        return $$"""
            Bạn là chuyên gia xuất đề thi. Hãy sinh {{request.Count}} câu hỏi trắc nghiệm/ tự luận dựa trên nội dung tài liệu sau.
            
            Yêu cầu:
            - Mức độ Bloom: {{bloomDesc}}{{topicPart}}
            - {{langPart}}
            - Mỗi câu hỏi phải có: nội dung câu hỏi, đáp án đúng, giải thích ngắn gọn
            
            Nội dung tài liệu:
            {{context}}
            
            Trả về JSON array với format:
            [
              {
                "questionText": "...",
                "answerKey": "...",
                "explanation": "...",
                "bloomLevel": {{(int?)request.BloomLevel ?? 3}},
                "topic": "{{request.Topic ?? ""}}"
              }
            ]
            """;
    }

    private async Task<string> CallOllamaGenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_ollamaOptions.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(5);

        var request = new
        {
            model = _ollamaOptions.GenerationModel,
            prompt = prompt,
            stream = false,
            format = "json",
            options = new { temperature = 0.3, top_p = 0.9 }
        };

        var response = await client.PostAsJsonAsync("/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        return result?.Response ?? "[]";
    }

    private List<GeneratedQuestion> ParseQuestionsResponse(string json, int documentId, int courseId, GenerateQuestionsRequest request)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<QuestionDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            return items.Select((item, index) => new GeneratedQuestion
            {
                DocumentId = documentId,
                CourseId = courseId,
                QuestionText = item.QuestionText ?? string.Empty,
                AnswerKey = item.AnswerKey,
                Explanation = item.Explanation,
                BloomLevel = Enum.IsDefined(typeof(BloomLevel), item.BloomLevel) ? (BloomLevel)item.BloomLevel : request.BloomLevel ?? BloomLevel.Apply,
                Topic = item.Topic,
                Status = QuestionStatus.PendingReview
            }).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse questions JSON: {Json}", json);
            throw new BusinessValidationException("AI trả về định dạng không hợp lệ. Vui lòng thử lại.");
        }
    }

    private sealed record QuestionDto(string? QuestionText, string? AnswerKey, string? Explanation, int BloomLevel, string? Topic);
    private sealed record OllamaGenerateResponse(string Response);
}