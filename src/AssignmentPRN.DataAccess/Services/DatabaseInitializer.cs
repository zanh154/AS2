using AssignmentPRN.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssignmentPRN.DataAccess.Services;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Startup check only. The schema and its seed data are owned by the team's shared
/// database, so this never creates tables and never writes rows — it just confirms
/// the connection works and reports what is missing.
/// </summary>
public sealed class DatabaseInitializer(
    AivesDbContext context,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("Không kết nối được cơ sở dữ liệu. Kiểm tra ConnectionStrings:DefaultConnection.");
            return;
        }

        var courses = await context.Courses.CountAsync(cancellationToken);
        var lecturers = await context.Users.CountAsync(user => user.Role.RoleName == "Lecturer", cancellationToken);
        var students = await context.Users.CountAsync(user => user.Role.RoleName == "Student", cancellationToken);

        logger.LogInformation(
            "Đã kết nối cơ sở dữ liệu: {Courses} môn học, {Lecturers} giảng viên, {Students} sinh viên.",
            courses,
            lecturers,
            students);

        if (courses == 0)
        {
            logger.LogWarning("Chưa có môn học nào trong bảng courses — không thể tạo lịch thi cho tới khi có.");
        }
    }
}
