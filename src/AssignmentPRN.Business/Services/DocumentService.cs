using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Repositories;
using DaEntities = AssignmentPRN.DataAccess.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AssignmentPRN.Business.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly DocumentStorageOptions _storageOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DocumentProcessingQueue _processingQueue;
    private readonly ILogger<DocumentService> _logger;
    private readonly IDocumentActor _actor;
    private readonly ICourseRepository _courses;
    private readonly RagOptions _rag;

    public DocumentService(
        IDocumentRepository documentRepository,
        ICatalogRepository catalogRepository,
        IOptions<DocumentStorageOptions> storageOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IHttpClientFactory httpClientFactory,
        DocumentProcessingQueue processingQueue,
        ILogger<DocumentService> logger, IDocumentActor actor, ICourseRepository courses, IOptions<RagOptions> rag)
    {
        _documentRepository = documentRepository;
        _catalogRepository = catalogRepository;
        _storageOptions = storageOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _httpClientFactory = httpClientFactory;
        _processingQueue = processingQueue;
        _logger = logger;
        _actor = actor; _courses = courses; _rag = rag.Value;
    }

    public async Task<ServiceResponse<DocumentResponse>> UploadAsync(int courseId, string fileName, Stream content, long fileSize, string contentType, int userId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            await EnsureCourseAccessAsync(courseId, cancellationToken);
            if (userId != _actor.UserId) throw new BusinessValidationException("Tài khoản tải lên không hợp lệ.");
            // Validate course exists and user has access
            var courseExists = await _catalogRepository.CourseExistsAsync(courseId, cancellationToken);
            if (!courseExists)
            {
                throw new BusinessValidationException("Không tìm thấy môn học.");
            }

            // Validate file
            ValidateFile(fileName, fileSize);

            // Generate unique filename and save
            var fileId = Guid.NewGuid().ToString("N")[..12];
            var extension = Path.GetExtension(fileName);
            var storedFileName = $"{fileId}{extension}";
            var relativePath = Path.Combine("documents", courseId.ToString(), storedFileName);
            var fullPath = Path.Combine(_storageOptions.BasePath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await content.CopyToAsync(stream, cancellationToken);
            }

            // Create document record
            var document = new DaEntities.Document
            {
                CourseId = courseId,
                UploadedById = userId,
                FileName = Path.GetFileName(fileName),
                ContentType = extension.ToLowerInvariant() switch { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation", _ => contentType },
                FileSize = fileSize,
                StoragePath = relativePath,
                Status = DaEntities.DocumentStatus.Pending
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
            await EnsureCourseAccessAsync(doc.CourseId, cancellationToken);

            var chunks = await _documentRepository.GetChunksByDocumentAsync(documentId, cancellationToken);
            var questions = await _documentRepository.GetQuestionsByDocumentAsync(documentId, cancellationToken);

            return MapToResponse(doc, chunks.Count, questions.Count);
        }, "Không thể tải tài liệu.");
    }

    public async Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<DocumentResponse>>(async () =>
        {
            await EnsureCourseAccessAsync(courseId, cancellationToken);
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
            await EnsureActorAsync(cancellationToken);
            if (_actor.Role != "Admin" && userId != _actor.UserId) throw new BusinessValidationException("Không có quyền xem tài liệu người khác.");
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
            await EnsureCourseAccessAsync(doc.CourseId, cancellationToken);

            if (userId != _actor.UserId) throw new BusinessValidationException("Tài khoản không hợp lệ.");

            // Delete physical file
            var fullPath = ResolvePath(doc.StoragePath);
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
            await EnsureCourseAccessAsync(doc.CourseId, cancellationToken);

            if (doc.Status != DaEntities.DocumentStatus.Completed)
            {
                throw new BusinessValidationException("Tài liệu chưa được xử lý xong. Vui lòng đợi xử lý hoàn tất.");
            }

            // Get relevant chunks for context (use first few chunks as context)
            BusinessValidation.InRange(request.Count, 1, 20, "Số câu hỏi");
            if (request.BloomLevel.HasValue && !Enum.IsDefined(request.BloomLevel.Value)) throw new BusinessValidationException("Mức độ Bloom không hợp lệ.");
            var embedding = await EmbedAsync(request.Topic ?? "Nội dung kiến thức chính trong tài liệu", cancellationToken);
            var chunks = await _documentRepository.SearchSimilarChunksAsync(embedding, doc.CourseId, _rag.TopK, _rag.SimilarityThreshold, cancellationToken, documentId);
            if (chunks.Count == 0) chunks = await _documentRepository.SearchSimilarChunksAsync(embedding, doc.CourseId, _rag.TopK, -1, cancellationToken, documentId);
            if (chunks.Count == 0) throw new BusinessValidationException("Tài liệu chưa có nội dung để sinh câu hỏi.");
            var contextChunks = chunks.Select(c => c.Content).ToList();
            var context = string.Join("\n\n---\n\n", contextChunks);

            // Build prompt
            var prompt = BuildGenerationPrompt(context, request);

            // Call Ollama
            var questionsJson = await CallOllamaGenerateAsync(prompt, request.Count, cancellationToken);

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
            await EnsureCourseAccessAsync(question.CourseId ?? question.Document.CourseId, cancellationToken);

            return MapQuestionToResponse(question);
        }, "Không thể tải câu hỏi.");
    }

    public async Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<GeneratedQuestionResponse>>(async () =>
        {
            var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken) ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");
            await EnsureCourseAccessAsync(doc.CourseId, cancellationToken);
            var questions = await _documentRepository.GetQuestionsByDocumentAsync(documentId, cancellationToken);
            return questions.Select(MapQuestionToResponse).ToList();
        }, "Không thể tải danh sách câu hỏi.");
    }

    public async Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByCourseAsync(int courseId, Interfaces.GeneratedQuestionStatus? status = null, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync<IReadOnlyList<GeneratedQuestionResponse>>(async () =>
        {
            await EnsureCourseAccessAsync(courseId, cancellationToken);
            var questions = await _documentRepository.GetQuestionsByCourseAsync(courseId, status?.ToDataAccess(), cancellationToken);
            return questions.Select(MapQuestionToResponse).ToList();
        }, "Không thể tải danh sách câu hỏi.");
    }

    public async Task<ServiceResponse> ReviewQuestionAsync(int questionId, int reviewerId, ReviewQuestionRequest request, CancellationToken cancellationToken = default)
    {
        return await ServiceExecutor.RunAsync(async () =>
        {
            var question = await _documentRepository.GetQuestionByIdAsync(questionId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy câu hỏi.");
            await EnsureCourseAccessAsync(question.CourseId ?? question.Document.CourseId, cancellationToken);

            if (reviewerId != _actor.UserId || request.Status is not (Interfaces.GeneratedQuestionStatus.Approved or Interfaces.GeneratedQuestionStatus.Rejected))
                throw new BusinessValidationException("Quyết định duyệt không hợp lệ.");
            await _documentRepository.ReviewAsync(questionId, current => {
                if (_actor.Role != "Admin" && current.Course?.LecturerId != _actor.UserId)
                    throw new BusinessValidationException("Bạn không có quyền duyệt câu hỏi.");
                if (current.Status != DaEntities.GeneratedQuestionStatus.PendingReview) throw new BusinessValidationException("Câu hỏi đã có quyết định duyệt.");
                if (request.QuestionText is not null) current.QuestionText = BusinessValidation.RequiredText(request.QuestionText, "Nội dung câu hỏi", 10000);
                if (request.AnswerKey is not null) current.AnswerKey = BusinessValidation.OptionalText(request.AnswerKey, "Đáp án", 2000);
                if (request.Explanation is not null) current.Explanation = BusinessValidation.OptionalText(request.Explanation, "Giải thích", 2000);
                if (request.Topic is not null) current.Topic = BusinessValidation.OptionalText(request.Topic, "Chủ đề", 200);
                if (request.BloomLevel is { } bloom) { if (!Enum.IsDefined(typeof(DaEntities.DocumentBloomLevel), (int)bloom)) throw new BusinessValidationException("Bloom không hợp lệ."); current.BloomLevel = bloom.ToDataAccess(); }
                current.Status = request.Status.ToDataAccess(); current.ReviewedById = reviewerId; current.ReviewedAt = DateTime.UtcNow;
                current.ReviewNotes = BusinessValidation.OptionalText(request.Notes, "Ghi chú", 1000);
            }, cancellationToken);
        }, "Không thể duyệt câu hỏi.");
    }

    private void ValidateFile(string fileName, long fileSize)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileSize <= 0)
        {
            throw new BusinessValidationException("Vui lòng chọn file.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_storageOptions.AllowedExtensions.Contains(extension))
        {
            throw new BusinessValidationException($"Định dạng file không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", _storageOptions.AllowedExtensions)}");
        }

        var maxSizeBytes = _storageOptions.MaxFileSizeMB * 1024L * 1024L;
        if (fileSize > maxSizeBytes)
        {
            throw new BusinessValidationException($"File quá lớn. Kích thước tối đa: {_storageOptions.MaxFileSizeMB} MB.");
        }
    }

    private static DocumentResponse MapToResponse(DaEntities.Document doc, int chunkCount, int questionCount) => new(
        doc.DocumentId,
        doc.CourseId,
        doc.Course?.CourseCode ?? string.Empty,
        doc.Course?.CourseName ?? string.Empty,
        doc.UploadedById,
        doc.UploadedBy?.FullName ?? string.Empty,
        doc.FileName,
        doc.ContentType,
        doc.FileSize,
        doc.Status.ToBusiness(),
        doc.ErrorMessage,
        doc.CreatedAt,
        doc.ProcessedAt,
        chunkCount,
        questionCount
    );

    private static GeneratedQuestionResponse MapQuestionToResponse(DaEntities.GeneratedQuestion q) => new(
        q.QuestionId,
        q.DocumentId,
        q.CourseId,
        q.QuestionText,
        q.AnswerKey,
        q.Explanation,
        q.BloomLevel.ToBusiness(),
        q.Topic,
        q.Status.ToBusiness(),
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
            Interfaces.DocumentBloomLevel.Remember => "Nhớ (Remember): Nhận biết, nhớ lại thông tin",
            Interfaces.DocumentBloomLevel.Understand => "Hiểu (Understand): Giải thích, tóm tắt, diễn giải",
            Interfaces.DocumentBloomLevel.Apply => "Áp dụng (Apply): Sử dụng kiến thức vào tình huống mới",
            Interfaces.DocumentBloomLevel.Analyze => "Phân tích (Analyze): Phân tách, so sánh, đối chiếu",
            Interfaces.DocumentBloomLevel.Evaluate => "Đánh giá (Evaluate): Phán xét, critique, bảo vệ quan điểm",
            Interfaces.DocumentBloomLevel.Create => "Sáng tạo (Create): Thiết kế, xây dựng, sáng tạo mới",
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

            Trả về JSON object với format:
            { "questions": [
              {
                "questionText": "...",
                "answerKey": "...",
                "explanation": "...",
                "bloomLevel": {{(int?)request.BloomLevel ?? 3}},
                "topic": "{{request.Topic ?? ""}}"
              }
            ] }
            """;
    }

    private async Task<string> CallOllamaGenerateAsync(string prompt, int questionCount, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_ollamaOptions.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(10);

        // NOTE: qwen3.5 is a thinking model. The strict JSON-schema `format`
        // makes it return an empty response, and leaving thinking on makes it
        // too slow on CPU. "think": false + plain "format": "json" works.
        // num_predict scales with the requested count: one question is about
        // 250 tokens, so a fixed 2048 budget only makes small requests wait
        // for tokens they never use.
        var request = new
        {
            model = _ollamaOptions.GenerationModel,
            prompt = prompt,
            stream = false,
            think = false,
            format = "json",
            options = new { temperature = 0.3, top_p = 0.9, num_ctx = 4096, num_predict = Math.Min(2048, 256 + questionCount * 300) }
        };

        var response = await client.PostAsJsonAsync("/api/generate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
        return result?.Response ?? "[]";
    }

    private List<DaEntities.GeneratedQuestion> ParseQuestionsResponse(string json, int documentId, int courseId, GenerateQuestionsRequest request)
    {
        try
        {
            json = StripMarkdownFences(json);
            using var parsed = JsonDocument.Parse(json);
            var list = parsed.RootElement.ValueKind == JsonValueKind.Array ? parsed.RootElement : parsed.RootElement.GetProperty("questions");
            var items = JsonSerializer.Deserialize<List<QuestionDto>>(list.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            if (items.Count == 0 || items.Any(x => string.IsNullOrWhiteSpace(x.QuestionText) || string.IsNullOrWhiteSpace(x.AnswerKey))) throw new BusinessValidationException("AI chưa tạo được câu hỏi hợp lệ.");
            return items.Take(request.Count).Select((item, index) => new DaEntities.GeneratedQuestion
            {
                DocumentId = documentId,
                CourseId = courseId,
                QuestionText = item.QuestionText ?? string.Empty,
                AnswerKey = item.AnswerKey,
                Explanation = item.Explanation,
                BloomLevel = Enum.IsDefined(typeof(DaEntities.DocumentBloomLevel), item.BloomLevel) ? (DaEntities.DocumentBloomLevel)item.BloomLevel : (request.BloomLevel?.ToDataAccess() ?? DaEntities.DocumentBloomLevel.Apply),
                Topic = item.Topic,
                Status = DaEntities.GeneratedQuestionStatus.PendingReview
            }).ToList();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            _logger.LogError(ex, "Failed to parse questions JSON: {Json}", json);
            throw new BusinessValidationException("AI trả về định dạng không hợp lệ. Vui lòng thử lại.");
        }
    }

    private sealed record QuestionDto(string? QuestionText, string? AnswerKey, string? Explanation, int BloomLevel, string? Topic);
    private sealed record OllamaGenerateResponse(string Response);

    private static string StripMarkdownFences(string json)
    {
        var text = json.Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text[start..(end + 1)];
        }

        return text;
    }

    private async Task EnsureActorAsync(CancellationToken ct) {
        if (_actor.UserId <= 0 || _actor.Role is not ("Admin" or "Lecturer") || !await _catalogRepository.UserIsInRoleAsync(_actor.UserId, _actor.Role!, ct))
            throw new BusinessValidationException("Bạn không có quyền quản lý tài liệu.");
    }
    private async Task EnsureCourseAccessAsync(int courseId, CancellationToken ct) {
        await EnsureActorAsync(ct);
        var course = await _courses.GetAsync(courseId, ct) ?? throw new BusinessValidationException("Không tìm thấy môn học.");
        if (_actor.Role != "Admin" && course.LecturerId != _actor.UserId) throw new BusinessValidationException("Bạn không có quyền quản lý tài liệu của môn này.");
    }
    private string ResolvePath(string storedPath) {
        var root = Path.GetFullPath(_storageOptions.BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, storedPath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new BusinessValidationException("Đường dẫn tài liệu không hợp lệ.");
        return path;
    }
    private async Task<float[]> EmbedAsync(string text, CancellationToken ct) {
        var client = _httpClientFactory.CreateClient(); client.BaseAddress = new Uri(_ollamaOptions.BaseUrl); client.Timeout = TimeSpan.FromMinutes(3);
        using var response = await client.PostAsJsonAsync("/api/embeddings", new { model = _ollamaOptions.EmbeddingModel, prompt = text }, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        return data?.Embedding is { Length: > 0 } vector ? vector : throw new BusinessValidationException("AI chưa tạo được embedding.");
    }
    private sealed record EmbeddingResponse(float[] Embedding);
}