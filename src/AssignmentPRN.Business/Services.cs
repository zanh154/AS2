using AssignmentPRN.DataAccess.Entities;

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
