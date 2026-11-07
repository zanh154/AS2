using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Contracts;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentPRN.DataAccess.Repositories;

public interface ICourseMaterialRepository
{
    Task<IReadOnlyList<CourseMaterialItem>> ListAsync(int? courseId, CancellationToken cancellationToken = default);

    /// <summary>Materials of the given courses, used to fill the topic dropdown on a question.</summary>
    Task<IReadOnlyList<CourseMaterialItem>> ListForCoursesAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken cancellationToken = default);

    Task<CourseMaterialItem?> GetAsync(int materialId, CancellationToken cancellationToken = default);

    /// <summary>True when the same file name is already stored for the course, ignoring case.</summary>
    Task<bool> FileNameExistsAsync(
        int courseId,
        string fileName,
        int exceptId,
        CancellationToken cancellationToken = default);

    Task<CourseMaterial> SaveAsync(CourseMaterial material, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a material to another course and/or renames it. The file on disk is not
    /// touched: <c>file_path</c> is a GUID and <c>file_name</c> is only the label.
    /// </summary>
    Task UpdateAsync(
        int materialId,
        int courseId,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int materialId, CancellationToken cancellationToken = default);

    Task<int> CountQuestionsAsync(int materialId, CancellationToken cancellationToken = default);
}

public class CourseMaterialRepository(AivesDbContext context) : ICourseMaterialRepository
{
    public async Task<IReadOnlyList<CourseMaterialItem>> ListAsync(
        int? courseId,
        CancellationToken cancellationToken = default)
    {
        var materials = context.CourseMaterials.AsNoTracking().AsQueryable();

        if (courseId is int id)
        {
            materials = materials.Where(material => material.CourseId == id);
        }

        return await Project(materials)
            .OrderByDescending(material => material.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CourseMaterialItem>> ListForCoursesAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken cancellationToken = default)
    {
        if (courseIds.Count == 0)
        {
            return Array.Empty<CourseMaterialItem>();
        }

        var ids = courseIds.ToList();

        return await Project(context.CourseMaterials.AsNoTracking().Where(material => ids.Contains(material.CourseId)))
            .OrderBy(material => material.FileName)
            .ToListAsync(cancellationToken);
    }

    public Task<CourseMaterialItem?> GetAsync(int materialId, CancellationToken cancellationToken = default) =>
        materialId <= 0
            ? Task.FromResult<CourseMaterialItem?>(null)
            : Project(context.CourseMaterials.AsNoTracking().Where(material => material.MaterialId == materialId))
                .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> FileNameExistsAsync(
        int courseId,
        string fileName,
        int exceptId,
        CancellationToken cancellationToken = default) =>
        context.CourseMaterials.AnyAsync(
            material => material.MaterialId != exceptId
                && material.CourseId == courseId
                && material.FileName.ToUpper() == fileName.ToUpper(),
            cancellationToken);

    public async Task<CourseMaterial> SaveAsync(CourseMaterial material, CancellationToken cancellationToken = default)
    {
        context.CourseMaterials.Add(material);
        await context.SaveChangesAsync(cancellationToken);
        return material;
    }

    public async Task UpdateAsync(
        int materialId,
        int courseId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var material = await context.CourseMaterials.FindAsync([materialId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

        material.CourseId = courseId;
        material.FileName = fileName;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int materialId, CancellationToken cancellationToken = default)
    {
        var material = await context.CourseMaterials.FindAsync([materialId], cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");

        context.CourseMaterials.Remove(material);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountQuestionsAsync(int materialId, CancellationToken cancellationToken = default) =>
        context.Questions.CountAsync(
            question => question.SourceMaterialId == materialId
                && question.Status != AssignmentPRN.DataAccess.Enums.QuestionStatus.Archived,
            cancellationToken);

    private static IQueryable<CourseMaterialItem> Project(IQueryable<CourseMaterial> materials) => materials
        .Select(material => new CourseMaterialItem
        {
            MaterialId = material.MaterialId,
            CourseId = material.CourseId,
            CourseCode = material.Course.CourseCode,
            CourseName = material.Course.CourseName,
            FileName = material.FileName,
            FilePath = material.FilePath,
            FileType = material.FileType,
            FileSize = material.FileSize,
            UploaderName = material.Uploader.FullName,
            UploadedAt = material.UploadedAt,
            QuestionCount = material.Questions.Count(question => question.Status != AssignmentPRN.DataAccess.Enums.QuestionStatus.Archived)
        });
}
