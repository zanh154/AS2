using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Classes are the roster an exam session draws its students from, so a class always
/// belongs to exactly one course and one lecturer, and only holds Student accounts.
/// </summary>
public class AcademicClassService(
    IAcademicClassRepository repository,
    ICourseRepository courses,
    ICatalogRepository catalog) : IAcademicClassService
{
    private const string LecturerRole = "Lecturer";
    private const string StudentRole = "Student";

    public Task<ServiceResponse<IReadOnlyList<AcademicClassResponse>>> ListAsync(int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync<IReadOnlyList<AcademicClassResponse>>(async () => (await repository.ListAsync(ct))
            .Where(x => lecturerId == null || x.LecturerId == lecturerId)
            .Select(ToResponse)
            .ToList(), "Không tải được lớp học.");

    public Task<ServiceResponse<AcademicClassResponse>> GetAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () => ToResponse(await OwnedAsync(id, lecturerId, ct)), "Không tải được lớp học.");

    public Task<ServiceResponse<AcademicClassResponse>> SaveAsync(AcademicClassSaveRequest request, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var academicClass = request.ClassId == 0 ? new AcademicClass { CreatedAt = DateTime.Now }
                : await OwnedAsync(request.ClassId, lecturerId, ct);
            var code = BusinessValidation.NormalizeCode(request.ClassCode, "mã lớp", 50);
            if (await repository.CodeExistsAsync(code, request.ClassId, ct))
                throw new BusinessValidationException("Mã lớp đã tồn tại.");

            var owner = lecturerId ?? BusinessValidation.PositiveId(request.LecturerId, "giảng viên");
            if (!await catalog.UserIsInRoleAsync(owner, LecturerRole, ct))
                throw new BusinessValidationException("Giảng viên không hợp lệ hoặc đã ngừng hoạt động.");

            var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
            var course = await courses.GetAsync(courseId, ct)
                ?? throw new BusinessValidationException("Không tìm thấy môn học.");
            if (!course.IsActive)
                throw new BusinessValidationException("Môn học đã ngừng hoạt động.");
            // The exam session picks course and lecturer from the class, so they must agree here.
            if (course.LecturerId != owner)
                throw new BusinessValidationException($"Môn {course.CourseCode} không do giảng viên này phụ trách.");

            academicClass.ClassCode = code;
            academicClass.ClassName = BusinessValidation.RequiredText(request.ClassName, "tên lớp", 200);
            academicClass.CourseId = courseId;
            academicClass.LecturerId = owner;
            academicClass.IsActive = request.IsActive;
            academicClass.UpdatedAt = request.ClassId == 0 ? null : DateTime.Now;
            await repository.SaveAsync(academicClass, ct);
            return ToResponse(academicClass);
        }, "Không thể lưu lớp học.");

    public Task<ServiceResponse> DeleteAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await OwnedAsync(id, lecturerId, ct);
            await repository.DeleteAsync(id, ct);
        }, "Không thể xoá lớp học.");

    public Task<ServiceResponse<AcademicClassRosterResponse>> GetRosterAsync(int id, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var academicClass = await OwnedRosterAsync(id, lecturerId, ct);
            return new AcademicClassRosterResponse
            {
                Class = ToResponse(academicClass),
                Students = academicClass.Students
                    .OrderBy(item => item.Student.FullName)
                    .ThenBy(item => item.Student.Email)
                    .Select(item => new ClassStudentResponse
                    {
                        StudentId = item.StudentId,
                        FullName = item.Student.FullName,
                        Email = item.Student.Email,
                        JoinedAt = item.JoinedAt
                    })
                    .ToList()
            };
        }, "Không tải được danh sách sinh viên.");

    public Task<ServiceResponse> AddStudentAsync(int classId, int studentId, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await OwnedAsync(classId, lecturerId, ct);
            var id = BusinessValidation.PositiveId(studentId, "sinh viên");
            if (!await catalog.UserIsInRoleAsync(id, StudentRole, ct))
                throw new BusinessValidationException("Tài khoản được chọn không phải sinh viên đang hoạt động.");
            await repository.AddStudentAsync(classId, id, ct);
        }, "Không thể thêm sinh viên vào lớp.");

    public Task<ServiceResponse> RemoveStudentAsync(int classId, int studentId, int? lecturerId, CancellationToken ct = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await OwnedAsync(classId, lecturerId, ct);
            await repository.RemoveStudentAsync(classId, BusinessValidation.PositiveId(studentId, "sinh viên"), ct);
        }, "Không thể xoá sinh viên khỏi lớp.");

    private async Task<AcademicClass> OwnedAsync(int id, int? lecturerId, CancellationToken ct) =>
        Owned(await repository.GetAsync(id, ct), lecturerId);

    private async Task<AcademicClass> OwnedRosterAsync(int id, int? lecturerId, CancellationToken ct) =>
        Owned(await repository.GetWithStudentsAsync(id, ct), lecturerId);

    private static AcademicClass Owned(AcademicClass? academicClass, int? lecturerId)
    {
        if (academicClass is null)
            throw new BusinessValidationException("Không tìm thấy lớp học.");
        if (lecturerId.HasValue && academicClass.LecturerId != lecturerId)
            throw new BusinessValidationException("Bạn không có quyền quản lý lớp học này.");
        return academicClass;
    }

    private static AcademicClassResponse ToResponse(AcademicClass academicClass) => new()
    {
        ClassId = academicClass.ClassId,
        ClassCode = academicClass.ClassCode,
        ClassName = academicClass.ClassName,
        CourseId = academicClass.CourseId,
        CourseCode = academicClass.Course?.CourseCode ?? string.Empty,
        CourseName = academicClass.Course?.CourseName ?? string.Empty,
        LecturerId = academicClass.LecturerId,
        LecturerName = academicClass.Lecturer?.FullName ?? string.Empty,
        IsActive = academicClass.IsActive,
        StudentCount = academicClass.Students.Count
    };
}
