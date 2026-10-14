using AssignmentPRN.Business.BusinessRules;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

public class CourseService(ICourseRepository repository, ICatalogRepository catalog, IExamRealtimeNotifier? realtime = null) : ICourseService
{
    public Task<ServiceResponse<IReadOnlyList<CourseResponse>>> ListAsync(int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync<IReadOnlyList<CourseResponse>>(async () => (await repository.ListAsync(ct))
            .Where(x => lecturerId == null || x.LecturerId == lecturerId)
            .Select(ToResponse)
            .ToList(), "Không tải được môn học.");

    public Task<ServiceResponse<CourseResponse>> GetAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => ToResponse(await OwnedAsync(id, lecturerId, ct)), "Không tải được môn học.");

    public Task<ServiceResponse<CourseResponse>> SaveAsync(CourseSaveRequest request, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var course = request.CourseId == 0 ? new Course { CreatedAt = DateTime.Now }
                : await OwnedAsync(request.CourseId, lecturerId, ct);
            var code = BusinessValidation.NormalizeCode(request.CourseCode, "mã môn", 50);
            if (await repository.CodeExistsAsync(code, request.CourseId, ct))
                throw new BusinessValidationException("Mã môn học đã tồn tại.");
            var owner = lecturerId ?? BusinessValidation.PositiveId(request.LecturerId, "giảng viên");
            if (!await catalog.UserIsInRoleAsync(owner, "Lecturer", ct))
                throw new BusinessValidationException("Giảng viên không hợp lệ hoặc đã ngừng hoạt động.");
            course.CourseCode = code;
            course.CourseName = BusinessValidation.RequiredText(request.CourseName, "tên môn", 200);
            course.Description = BusinessValidation.OptionalText(request.Description, "mô tả", 1000);
            course.LecturerId = owner;
            course.IsActive = request.IsActive;
            course.UpdatedAt = request.CourseId == 0 ? null : DateTime.Now;
            await repository.SaveAsync(course, ct);
            if (realtime is not null) await realtime.ChangedAsync(new(0, owner, [], "courses"), ct);
            return ToResponse(course);
        }, "Không thể lưu môn học.");

    public Task<ServiceResponse> DeleteAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var course = await OwnedAsync(id, lecturerId, ct);
            await repository.DeleteAsync(id, ct);
            if (realtime is not null) await realtime.ChangedAsync(new(0, course.LecturerId, [], "courses"), ct);
        }, "Không thể xoá môn học.");

    private async Task<Course> OwnedAsync(int id, int? lecturerId, CancellationToken ct)
    {
        var course = await repository.GetAsync(id, ct)
            ?? throw new BusinessValidationException("Không tìm thấy môn học.");
        if (lecturerId.HasValue && course.LecturerId != lecturerId)
            throw new BusinessValidationException("Bạn không có quyền quản lý môn học này.");
        return course;
    }

    private static CourseResponse ToResponse(Course course) => new()
    {
        CourseId = course.CourseId,
        CourseCode = course.CourseCode,
        CourseName = course.CourseName,
        Description = course.Description,
        LecturerId = course.LecturerId,
        LecturerName = course.Lecturer?.FullName ?? string.Empty,
        IsActive = course.IsActive
    };
}
