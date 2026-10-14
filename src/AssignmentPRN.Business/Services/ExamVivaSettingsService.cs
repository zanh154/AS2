using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Repositories;
using Settings = AssignmentPRN.Business.Interfaces.ExamVivaSettings;
namespace AssignmentPRN.Business.Services;
public sealed class ExamVivaSettingsService(IExamSessionRepository sessions, IExamVivaSettingsRepository settings, IExamRealtimeNotifier? realtime = null) : IExamVivaSettingsService
{
    public Task<ServiceResponse<Settings>> GetAsync(int examId, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => {
            var state = await sessions.GetDetailAsync(examId, ct) ?? throw new BusinessValidationException("Không tìm thấy phiên thi.");
            Own(state.Lecturer.UserId, lecturerId);
            var row = await settings.GetAsync(examId, ct);
            return row is null ? new Settings(Math.Min(120, state.TimePerStudent * 60), 1) : new Settings(row.SecondsPerQuestion, row.MaxFollowUpsPerQuestion);
        }, "Không thể tải cấu hình vấn đáp. Kiểm tra migration ASM2 đã được chạy.");
    public Task<ServiceResponse> SaveAsync(int examId, Settings value, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => {
            BusinessValidation.InRange(value.SecondsPerQuestion, 15, 1800, "Thời gian mỗi câu (giây)");
            BusinessValidation.InRange(value.MaxFollowUpsPerQuestion, 0, 5, "Số lượt hỏi xoáy mỗi câu");
            AssignmentPRN.DataAccess.Contracts.ExamSessionDetail? saved = null;
            await settings.SaveAsync(examId, value.SecondsPerQuestion, value.MaxFollowUpsPerQuestion, (state, issued) => {
                Own(state.Lecturer.UserId, lecturerId);
                if (!ExamSessionRules.CanEdit(state.Status.ToBusiness()) || state.Candidates.Any(x => x.Status.ToBusiness() != CandidateStatus.Waiting) || issued)
                    throw new BusinessValidationException("Chỉ được sửa cấu hình trước khi phát đề và khi tất cả sinh viên còn chờ thi.");
                if (value.SecondsPerQuestion > state.TimePerStudent * 60)
                    throw new BusinessValidationException("Thời gian mỗi câu không được vượt thời lượng lượt thi.");
                saved = state;
            }, ct);
            if (realtime is not null && saved is not null) await realtime.ChangedAsync(new(examId, saved.Lecturer.UserId, saved.Candidates.Select(x => x.StudentId).ToArray(), "settings"), ct);
        }, "Không thể lưu cấu hình vấn đáp.");
    private static void Own(int owner, int? lecturer) {
        if (lecturer.HasValue && owner != lecturer) throw new BusinessValidationException("Bạn không có quyền quản lý phiên thi này.");
    }
}
