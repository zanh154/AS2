using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

public sealed class ClassManagementService(ICatalogRepository catalogRepository) : IClassManagementService
{
    private const string StudentRole = "Student";

    public Task<ServiceResponse<IReadOnlyList<AcademicClassResponse>>> ListAsync(
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync<IReadOnlyList<AcademicClassResponse>>(
            async () => (await catalogRepository.ListActiveClassesAsync(cancellationToken))
                .Select(item => new AcademicClassResponse
                {
                    ClassId = item.ClassId,
                    ClassCode = item.ClassCode,
                    ClassName = item.ClassName,
                    CourseId = item.CourseId,
                    CourseCode = item.Course.CourseCode,
                    CourseName = item.Course.CourseName,
                    LecturerId = item.LecturerId,
                    LecturerName = item.Lecturer.FullName,
                    StudentCount = item.Students.Count(student =>
                        student.Student.Status == "Active"
                        && student.Student.Role.RoleName == StudentRole)
                })
                .ToList(),
            "Không thể tải danh sách lớp.");

    public Task<ServiceResponse<ClassRosterResponse>> GetAsync(
        int classId,
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(
            async () => Map(await GetClassAsync(classId, cancellationToken)),
            "Không thể tải lớp học.");

    public Task<ServiceResponse> AddStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await GetClassAsync(classId, cancellationToken);
            BusinessValidation.PositiveId(studentId, "sinh viên");

            if (!await catalogRepository.UserIsInRoleAsync(studentId, StudentRole, cancellationToken))
            {
                throw new BusinessValidationException("Tài khoản được chọn không phải sinh viên đang hoạt động.");
            }

            if (await catalogRepository.ClassStudentExistsAsync(classId, studentId, cancellationToken))
            {
                throw new BusinessValidationException("Sinh viên đã có trong lớp.");
            }

            await catalogRepository.AddClassStudentAsync(classId, studentId, cancellationToken);
        }, "Không thể thêm sinh viên vào lớp.");

    public Task<ServiceResponse> RemoveStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            await GetClassAsync(classId, cancellationToken);
            BusinessValidation.PositiveId(studentId, "sinh viên");
            if (!await catalogRepository.RemoveClassStudentAsync(classId, studentId, cancellationToken))
            {
                throw new BusinessValidationException("Sinh viên không có trong lớp.");
            }
        }, "Không thể xoá sinh viên khỏi lớp.");

    private async Task<AssignmentPRN.DataAccess.Entities.AcademicClass> GetClassAsync(
        int classId,
        CancellationToken cancellationToken)
    {
        var id = BusinessValidation.PositiveId(classId, "lớp học");
        return await catalogRepository.GetClassWithStudentsAsync(id, cancellationToken)
            ?? throw new BusinessValidationException("Không tìm thấy lớp học đang hoạt động.");
    }

    private static ClassRosterResponse Map(AssignmentPRN.DataAccess.Entities.AcademicClass item) => new()
    {
        ClassId = item.ClassId,
        CourseId = item.CourseId,
        LecturerId = item.LecturerId,
        ClassCode = item.ClassCode,
        ClassName = item.ClassName,
        Students = item.Students
            .Select(row => row.Student)
            .Where(student => student.Status == "Active" && student.Role.RoleName == StudentRole)
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.Email)
            .Select(student => new PersonResponse
            {
                UserId = student.UserId,
                FullName = student.FullName,
                Email = student.Email
            })
            .ToList()
    };
}
