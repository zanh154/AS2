namespace AssignmentPRN.Business.Interfaces;

public sealed record DocumentResponse(
    int DocumentId,
    int CourseId,
    string CourseCode,
    string CourseName,
    int UploadedById,
    string UploadedByName,
    string FileName,
    string ContentType,
    long FileSize,
    DocumentStatus Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    int ChunkCount,
    int QuestionCount
);

public sealed record GenerateQuestionsRequest(
    int Count,
    DocumentBloomLevel? BloomLevel,
    string? Topic,
    string Language = "vi"
);

public sealed record ReviewQuestionRequest(
    GeneratedQuestionStatus Status,
    string? Notes,
    string? QuestionText = null,
    string? AnswerKey = null,
    string? Explanation = null,
    DocumentBloomLevel? BloomLevel = null,
    string? Topic = null
);

public sealed record GeneratedQuestionResponse(
    int QuestionId,
    int DocumentId,
    int? CourseId,
    string QuestionText,
    string? AnswerKey,
    string? Explanation,
    DocumentBloomLevel BloomLevel,
    string? Topic,
    GeneratedQuestionStatus Status,
    int? ReviewedById,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    DateTime CreatedAt
);

public interface IDocumentService
{
    /// <summary>
    /// Stores a file already opened by the Presentation layer. The controller owns
    /// <see cref="IFormFile"/>; Business only sees the file name, an open stream,
    /// its size and content type — mirroring <c>ICourseMaterialService.UploadAsync</c>.
    /// </summary>
    Task<ServiceResponse<DocumentResponse>> UploadAsync(
        int courseId,
        string fileName,
        Stream content,
        long fileSize,
        string contentType,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<DocumentResponse>> GetAsync(int documentId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(int documentId, int userId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> GenerateQuestionsAsync(int documentId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResponse<GeneratedQuestionResponse>> GetQuestionAsync(int questionId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByCourseAsync(int courseId, GeneratedQuestionStatus? status = null, CancellationToken cancellationToken = default);

    Task<ServiceResponse> ReviewQuestionAsync(int questionId, int reviewerId, ReviewQuestionRequest request, CancellationToken cancellationToken = default);
}
