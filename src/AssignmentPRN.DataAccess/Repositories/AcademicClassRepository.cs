using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IAcademicClassRepository
{
    Task<List<AcademicClass>> ListAsync(CancellationToken ct = default);
    Task<AcademicClass?> GetAsync(int id, CancellationToken ct = default);
    Task<AcademicClass?> GetWithStudentsAsync(int id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken ct = default);
    Task SaveAsync(AcademicClass academicClass, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task AddStudentAsync(int classId, int studentId, CancellationToken ct = default);
    Task RemoveStudentAsync(int classId, int studentId, CancellationToken ct = default);
}

public class AcademicClassRepository(AivesDbContext context) : IAcademicClassRepository
{
    public Task<List<AcademicClass>> ListAsync(CancellationToken ct = default) => context.AcademicClasses
        .AsNoTracking()
        .Include(x => x.Course)
        .Include(x => x.Lecturer)
        .Include(x => x.Students)
        .AsSplitQuery()
        .OrderBy(x => x.ClassCode)
        .ToListAsync(ct);

    public Task<AcademicClass?> GetAsync(int id, CancellationToken ct = default) => context.AcademicClasses
        .AsNoTracking().FirstOrDefaultAsync(x => x.ClassId == id, ct);

    public Task<AcademicClass?> GetWithStudentsAsync(int id, CancellationToken ct = default) =>
        context.AcademicClasses
            .AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.Lecturer)
            .Include(x => x.Students)
                .ThenInclude(x => x.Student)
                    .ThenInclude(x => x.Role)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.ClassId == id, ct);

    public Task<bool> CodeExistsAsync(string code, int exceptId, CancellationToken ct = default) =>
        context.AcademicClasses.AnyAsync(x => x.ClassId != exceptId && x.ClassCode.ToUpper() == code.ToUpper(), ct);

    public async Task SaveAsync(AcademicClass academicClass, CancellationToken ct = default)
    {
        if (academicClass.ClassId == 0) context.AcademicClasses.Add(academicClass);
        else
        {
            var existing = await context.AcademicClasses.FindAsync([academicClass.ClassId], ct)
                ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");
            context.Entry(existing).CurrentValues.SetValues(academicClass);
        }
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ArgumentException("Không thể lưu lớp học. Kiểm tra mã lớp không trùng, môn học và giảng viên hợp lệ.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var academicClass = await context.AcademicClasses.Include(x => x.Students)
            .FirstOrDefaultAsync(x => x.ClassId == id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy lớp học.");
        if (academicClass.Students.Count > 0)
            throw new ArgumentException("Lớp còn sinh viên. Hãy xoá hết sinh viên hoặc ngừng hoạt động thay vì xoá lớp.");
        context.AcademicClasses.Remove(academicClass);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ArgumentException("Lớp học đang được sử dụng. Hãy ngừng hoạt động thay vì xoá.");
        }
    }

    public async Task AddStudentAsync(int classId, int studentId, CancellationToken ct = default)
    {
        if (await context.ClassStudents.AnyAsync(x => x.ClassId == classId && x.StudentId == studentId, ct))
            throw new ArgumentException("Sinh viên đã có trong lớp này.");
        context.ClassStudents.Add(new ClassStudent { ClassId = classId, StudentId = studentId, JoinedAt = DateTime.Now });
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ArgumentException("Không thể thêm sinh viên vào lớp.");
        }
    }

    public async Task RemoveStudentAsync(int classId, int studentId, CancellationToken ct = default)
    {
        var membership = await context.ClassStudents
            .FirstOrDefaultAsync(x => x.ClassId == classId && x.StudentId == studentId, ct)
            ?? throw new KeyNotFoundException("Sinh viên không có trong lớp này.");
        context.ClassStudents.Remove(membership);
        await context.SaveChangesAsync(ct);
    }
}
