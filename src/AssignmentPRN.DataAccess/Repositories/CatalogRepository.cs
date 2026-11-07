using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public class CatalogRepository(AivesDbContext context) : ICatalogRepository
{
    private const string ActiveStatus = "Active";

    public async Task<IReadOnlyList<Course>> ListActiveCoursesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Courses
            .AsNoTracking()
            .Where(course => course.IsActive)
            .OrderBy(course => course.CourseCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AcademicClass>> ListActiveClassesAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.AcademicClasses
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Include(item => item.Course)
            .Include(item => item.Lecturer)
            .Include(item => item.Students)
                .ThenInclude(item => item.Student)
                    .ThenInclude(item => item.Role)
            .AsSplitQuery()
            .OrderBy(item => item.ClassCode)
            .ToListAsync(cancellationToken);
    }

    public Task<AcademicClass?> GetClassWithStudentsAsync(
        int classId,
        CancellationToken cancellationToken = default)
    {
        return context.AcademicClasses
            .AsNoTracking()
            .Include(item => item.Course)
            .Include(item => item.Lecturer)
            .Include(item => item.Students)
                .ThenInclude(item => item.Student)
                    .ThenInclude(item => item.Role)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.ClassId == classId && item.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListUsersInRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Role.RoleName == roleName && user.Status == ActiveStatus)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> SearchUsersAsync(
        string roleName,
        string term,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var needle = term.Trim();

        var query = context.Users
            .AsNoTracking()
            .Where(user => user.Role.RoleName == roleName
                && user.Status == ActiveStatus);

        if (!string.IsNullOrEmpty(needle))
        {
            query = query.Where(user =>
                user.Email.Contains(needle) || user.FullName.Contains(needle));
        }

        return await query
            .OrderBy(user => user.Email)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> FindUsersByEmailsAsync(
        string roleName,
        IReadOnlyCollection<string> emails,
        CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return Array.Empty<User>();
        }

        return await context.Users
            .AsNoTracking()
            .Where(user => user.Role.RoleName == roleName
                && user.Status == ActiveStatus
                && emails.Contains(user.Email.ToLower()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListStudentIdsInCourseAsync(
        int courseId,
        CancellationToken cancellationToken = default)
    {
        return await context.ClassStudents
            .AsNoTracking()
            .Where(item => item.Class.CourseId == courseId && item.Class.IsActive)
            .Select(item => item.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CourseExistsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return context.Courses.AnyAsync(course => course.CourseId == courseId && course.IsActive, cancellationToken);
    }

    public Task<bool> UserIsInRoleAsync(int userId, string roleName, CancellationToken cancellationToken = default)
    {
        return context.Users.AnyAsync(
            user => user.UserId == userId && user.Role.RoleName == roleName && user.Status == ActiveStatus,
            cancellationToken);
    }

    public Task<bool> ClassStudentExistsAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default) =>
        context.ClassStudents.AnyAsync(
            item => item.ClassId == classId && item.StudentId == studentId,
            cancellationToken);

    public async Task AddClassStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = classId,
            StudentId = studentId,
            JoinedAt = DateTime.Now
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveClassStudentAsync(
        int classId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var item = await context.ClassStudents.FindAsync([classId, studentId], cancellationToken);
        if (item is null)
        {
            return false;
        }

        context.ClassStudents.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
