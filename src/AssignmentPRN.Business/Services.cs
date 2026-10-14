using AssignmentPRN.DataAccess.Entities;
using Microsoft.AspNetCore.Http;

namespace AssignmentPRN.Business;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);

    Task<User?> GetUserByIdAsync(int userId);

    Task<(bool Success, string? Error, User? User)> RegisterAsync(
        string fullName,
        string email,
        string password);
}

public interface IExamSessionService
{
    Task<ServiceResponse<IReadOnlyList<ExamSessionListItemResponse>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> GetAsync(
        int examId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionOptionsResponse>> GetCreationOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ClassRosterResponse>> GetClassRosterAsync(
        int classId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> CreateAsync(
        ExamSessionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ExamSessionDetailResponse>> RescheduleAsync(
        ExamSessionRescheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(int examId, CancellationToken cancellationToken = default);

    /// <summary>Lists student accounts, optionally filtering by name or email.</summary>
    Task<ServiceResponse<IReadOnlyList<PersonResponse>>> SearchStudentsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the exam schedule of one student account.</summary>
    Task<ServiceResponse<StudentScheduleResponse>> GetStudentScheduleAsync(
        int studentUserId,
        CancellationToken cancellationToken = default);
}

public interface IClassManagementService
{
    Task<ServiceResponse<IReadOnlyList<AcademicClassResponse>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ServiceResponse<ClassRosterResponse>> GetAsync(
        int classId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> AddStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse> RemoveStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default);
}

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
    BloomLevel? BloomLevel,
    string? Topic,
    string Language = "vi"
);

public sealed record ReviewQuestionRequest(
    QuestionStatus Status,
    string? Notes
);

public sealed record GeneratedQuestionResponse(
    int QuestionId,
    int DocumentId,
    int? CourseId,
    string QuestionText,
    string? AnswerKey,
    string? Explanation,
    BloomLevel BloomLevel,
    string? Topic,
    QuestionStatus Status,
    int? ReviewedById,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    DateTime CreatedAt
);

public interface IDocumentService
{
    Task<ServiceResponse<DocumentResponse>> UploadAsync(int courseId, int userId, IFormFile file, CancellationToken cancellationToken = default);

    Task<ServiceResponse<DocumentResponse>> GetAsync(int documentId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<DocumentResponse>>> ListByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<ServiceResponse> DeleteAsync(int documentId, int userId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> GenerateQuestionsAsync(int documentId, GenerateQuestionsRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResponse<GeneratedQuestionResponse>> GetQuestionAsync(int questionId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default);

    Task<ServiceResponse<IReadOnlyList<GeneratedQuestionResponse>>> ListQuestionsByCourseAsync(int courseId, QuestionStatus? status = null, CancellationToken cancellationToken = default);

    Task<ServiceResponse> ReviewQuestionAsync(int questionId, int reviewerId, ReviewQuestionRequest request, CancellationToken cancellationToken = default);
}
