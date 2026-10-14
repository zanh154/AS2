namespace AssignmentPRN.Business.Interfaces;
public sealed record ExamVivaSettings(int SecondsPerQuestion = 120, int MaxFollowUpsPerQuestion = 1);
public interface IExamVivaSettingsService
{
    Task<ServiceResponse<ExamVivaSettings>> GetAsync(int examId, int? lecturerId, CancellationToken ct = default);
    Task<ServiceResponse> SaveAsync(int examId, ExamVivaSettings settings, int? lecturerId, CancellationToken ct = default);
}
