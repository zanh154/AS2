using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.DataAccess.Repositories;

/// <summary>Read-only lookups for the courses and accounts an exam session refers to.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<Course>> ListActiveCoursesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademicClass>> ListActiveClassesAsync(CancellationToken cancellationToken = default);

    Task<AcademicClass?> GetClassWithStudentsAsync(int classId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>Lists users of a role, optionally filtering by an email or name fragment.</summary>
    Task<IReadOnlyList<User>> SearchUsersAsync(
        string roleName,
        string term,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves exact emails to accounts of a role. Emails must already be lowercased.</summary>
    Task<IReadOnlyList<User>> FindUsersByEmailsAsync(
        string roleName,
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default);

    /// <summary>Ids of the students enrolled in any active class of the course.</summary>
    Task<IReadOnlyList<int>> ListStudentIdsInCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken = default);

    Task<bool> UserIsInRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default);

    Task<bool> ClassStudentExistsAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task AddClassStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveClassStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default);
}
