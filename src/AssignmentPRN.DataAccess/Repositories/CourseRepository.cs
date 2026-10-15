using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public interface ICourseRepository
{
    Task<List<Course>> ListAsync(CancellationToken ct = default);
    Task<Course?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken ct = default);
    Task SaveAsync(Course course, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class CourseRepository(AivesDbContext context) : ICourseRepository
{
    public Task<List<Course>> ListAsync(CancellationToken ct = default) => context.Courses
        .AsNoTracking().Include(x => x.Lecturer).OrderBy(x => x.CourseCode).ToListAsync(ct);

    public Task<Course?> GetAsync(int id, CancellationToken ct = default) => context.Courses
        .AsNoTracking().FirstOrDefaultAsync(x => x.CourseId == id, ct);

    public Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken ct = default) =>
        context.Courses.AnyAsync(x => x.CourseId != exceptId && x.CourseCode.ToUpper() == code.ToUpper(), ct);

    public async Task SaveAsync(Course course, CancellationToken ct = default)
    {
        if (course.CourseId == 0) context.Courses.Add(course);
        else
        {
            var existing = await context.Courses.FindAsync([course.CourseId], ct)
                ?? throw new KeyNotFoundException("Không tìm thấy môn học.");
            context.Entry(existing).CurrentValues.SetValues(course);
        }
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ArgumentException("Không thể lưu môn học. Kiểm tra mã môn không trùng và giảng viên hợp lệ.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (await context.ExamSessions.AnyAsync(x => x.CourseId == id, ct)
            || await context.AcademicClasses.AnyAsync(x => x.CourseId == id, ct))
            throw new ArgumentException("Môn học đã có lớp hoặc phiên thi. Hãy ngừng hoạt động thay vì xoá.");
        var course = await context.Courses.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Không tìm thấy môn học.");
        context.Courses.Remove(course);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ArgumentException("Môn học đang được sử dụng. Hãy ngừng hoạt động thay vì xoá.");
        }
    }
}
