using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Repositories;

namespace AssignmentPRN.Business.Services;

/// <summary>Read-only lookup data exposed without leaking database entities to the UI.</summary>
public sealed class CatalogService(ICatalogRepository repository) : ICatalogService
{
    public Task<ServiceResponse<IReadOnlyList<CourseResponse>>> ListActiveCoursesAsync(
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync<IReadOnlyList<CourseResponse>>(
            async () => (await repository.ListActiveCoursesAsync(cancellationToken))
                .Select(course => new CourseResponse
                {
                    CourseId = course.CourseId,
                    CourseCode = course.CourseCode,
                    CourseName = course.CourseName,
                    Description = course.Description,
                    LecturerId = course.LecturerId,
                    IsActive = course.IsActive
                })
                .ToList(),
            "Không thể tải danh sách môn học.");

    public Task<ServiceResponse<IReadOnlyList<PersonResponse>>> ListActiveUsersInRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync<IReadOnlyList<PersonResponse>>(
            async () => (await repository.ListUsersInRoleAsync(roleName, cancellationToken))
                .Select(user => new PersonResponse
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email
                })
                .ToList(),
            "Không thể tải danh sách người dùng.");
}
