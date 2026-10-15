using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Enums;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Services;

using MaterialFileType = AssignmentPRN.Business.Interfaces.MaterialFileType;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Owns the course material list, which doubles as the topic a question is filed under.
/// The file itself is written to disk by the controller before this service records it.
/// </summary>
public class CourseMaterialService(
    ICourseMaterialRepository materialRepository,
    ICatalogRepository catalogRepository,
    IMaterialFileStore fileStore) : ICourseMaterialService
{
    /// <summary>Longest stored file name, matching course_materials.file_name.</summary>
    public const int MaxFileNameLength = 255;

    public Task<ServiceResponse<IReadOnlyList<CourseMaterialResponse>>> ListAsync(
        int? lecturerId,
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync<IReadOnlyList<CourseMaterialResponse>>(
            async () =>
            {
                var visibleCourseIds = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
                if (visibleCourseIds.Count == 0)
                {
                    return (IReadOnlyList<CourseMaterialResponse>)Array.Empty<CourseMaterialResponse>();
                }

                if (courseId is int requested && !visibleCourseIds.Contains(requested))
                {
                    throw new BusinessValidationException("Bạn không có quyền xem tài liệu của môn học này.");
                }

                // Listing by course is only worth the round trip when the caller picked one.
                var items = courseId.HasValue
                    ? await materialRepository.ListAsync(courseId, cancellationToken)
                    : await materialRepository.ListForCoursesAsync(visibleCourseIds, cancellationToken);

                return items
                    .Where(item => visibleCourseIds.Contains(item.CourseId))
                    .Select(MapMaterial)
                    .ToList();
            },
            "Không thể tải danh sách tài liệu.");
    }

    public Task<ServiceResponse<CourseMaterialResponse>> CreateAsync(
        CourseMaterialCreateRequest request,
        int uploaderId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                if (!await catalogRepository.CourseExistsAsync(courseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                var fileName = BusinessValidation.RequiredText(request.FileName, "tên tệp", MaxFileNameLength);
                var filePath = BusinessValidation.RequiredText(request.FilePath, "đường dẫn tệp", 1000);

                if (await materialRepository.FileNameExistsAsync(courseId, fileName, 0, cancellationToken))
                {
                    throw new BusinessValidationException(
                        $"Môn học này đã có tệp tên {fileName}. Hãy đổi tên hoặc xoá tệp cũ.");
                }

                var created = await materialRepository.SaveAsync(
                    new CourseMaterial
                    {
                        CourseId = courseId,
                        FileName = fileName,
                        FilePath = filePath,
                        FileType = request.FileType.ToDataAccess(),
                        FileSize = request.FileSize,
                        UploadedBy = uploaderId,
                        ProcessingStatus = MaterialProcessingStatus.Completed,
                        UploadedAt = DateTime.Now
                    },
                    cancellationToken);

                // Re-read so the response carries the course code and uploader name the
                // project query fills in, instead of guessing them here.
                var saved = await materialRepository.GetAsync(created.MaterialId, cancellationToken);
                return saved is null
                    ? throw new BusinessValidationException("Không thể đọc lại tài liệu vừa lưu.")
                    : MapMaterial(saved);
            },
            "Không thể lưu tài liệu.");
    }

    public async Task<ServiceResponse<CourseMaterialResponse>> UploadAsync(
        int courseId,
        string fileName,
        Stream content,
        long? fileSize,
        MaterialFileType fileType,
        int uploaderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var storedPath = await fileStore.SaveAsync(content, fileName, cancellationToken);
        var result = await CreateAsync(
            new CourseMaterialCreateRequest
            {
                CourseId = courseId,
                FileName = Path.GetFileName(fileName),
                FilePath = storedPath,
                FileType = fileType,
                FileSize = fileSize
            },
            uploaderId,
            cancellationToken);

        if (!result.Success)
        {
            await fileStore.DeleteAsync(storedPath, cancellationToken);
        }

        return result;
    }

    public Task<ServiceResponse<MaterialDownload>> DownloadAsync(
        int materialId,
        CancellationToken cancellationToken = default) =>
        ServiceExecutor.RunAsync(async () =>
        {
            var material = await materialRepository.GetAsync(materialId, cancellationToken)
                ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");
            var stream = await fileStore.OpenReadAsync(material.FilePath, cancellationToken)
                ?? throw new BusinessValidationException("Tệp không còn trên máy chủ.");

            return new MaterialDownload
            {
                Content = stream,
                FileName = material.FileName,
                FileType = material.FileType.ToBusiness()
            };
        }, "Không thể tải tài liệu.");

    public async Task<CourseMaterialResponse?> GetAsync(
        int materialId,
        CancellationToken cancellationToken = default)
    {
        if (materialId <= 0)
        {
            return null;
        }

        var material = await materialRepository.GetAsync(materialId, cancellationToken);
        return material is null ? null : MapMaterial(material);
    }

    public Task<ServiceResponse<CourseMaterialResponse>> UpdateAsync(
        CourseMaterialUpdateRequest request,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);

                var material = await materialRepository.GetAsync(request.MaterialId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");

                // Both ends are checked: a lecturer may not take a material out of a course
                // they do not own, nor push one into somebody else's course.
                await EnsureCourseVisibleAsync(material.CourseId, lecturerId, cancellationToken);

                var courseId = BusinessValidation.PositiveId(request.CourseId, "môn học");
                await EnsureCourseVisibleAsync(courseId, lecturerId, cancellationToken);
                if (!await catalogRepository.CourseExistsAsync(courseId, cancellationToken))
                {
                    throw new BusinessValidationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
                }

                var fileName = BusinessValidation.RequiredText(request.FileName, "tên tệp", MaxFileNameLength);

                // file_type was read from the extension at upload time and the bytes on disk
                // have not changed, so the extension may not change either.
                if (!string.Equals(
                        Path.GetExtension(fileName),
                        Path.GetExtension(material.FileName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessValidationException(
                        $"Phải giữ nguyên phần mở rộng {Path.GetExtension(material.FileName)} của tệp.");
                }

                if (courseId != material.CourseId)
                {
                    // A question and the topic it is filed under must stay in one course,
                    // and moving the questions too could break exams already dealt from them.
                    var questionCount = await materialRepository.CountQuestionsAsync(
                        request.MaterialId, cancellationToken);
                    if (questionCount > 0)
                    {
                        throw new BusinessValidationException(
                            $"Tài liệu này đang là chủ đề của {questionCount} câu hỏi thuộc môn "
                            + $"{material.CourseCode}, nên không đổi được sang môn khác. Hãy xoá hoặc "
                            + "ẩn các câu hỏi đó trước.");
                    }
                }

                if (await materialRepository.FileNameExistsAsync(
                        courseId, fileName, request.MaterialId, cancellationToken))
                {
                    throw new BusinessValidationException(
                        $"Môn học này đã có tệp tên {fileName}. Hãy đổi tên hoặc xoá tệp cũ.");
                }

                await materialRepository.UpdateAsync(
                    request.MaterialId, courseId, fileName, cancellationToken);

                var saved = await materialRepository.GetAsync(request.MaterialId, cancellationToken);
                return saved is null
                    ? throw new BusinessValidationException("Không thể đọc lại tài liệu vừa sửa.")
                    : MapMaterial(saved);
            },
            "Không thể sửa tài liệu.");
    }

    public Task<ServiceResponse> DeleteAsync(
        int materialId,
        int? lecturerId,
        CancellationToken cancellationToken = default)
    {
        return ServiceExecutor.RunAsync(
            async () =>
            {
                var material = await materialRepository.GetAsync(materialId, cancellationToken)
                    ?? throw new BusinessValidationException("Không tìm thấy tài liệu.");
                await EnsureCourseVisibleAsync(material.CourseId, lecturerId, cancellationToken);

                var questionCount = await materialRepository.CountQuestionsAsync(materialId, cancellationToken);
                if (questionCount > 0)
                {
                    throw new BusinessValidationException(
                        $"Tài liệu này đang được {questionCount} câu hỏi sử dụng. "
                        + "Hãy chuyển hoặc ẩn các câu hỏi đó trước khi xoá tài liệu.");
                }

                await materialRepository.DeleteAsync(materialId, cancellationToken);
                await fileStore.DeleteAsync(material.FilePath, cancellationToken);
            },
            "Không thể xoá tài liệu.");
    }

    private async Task<IReadOnlyList<int>> VisibleCourseIdsAsync(
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        var courses = await catalogRepository.ListActiveCoursesAsync(cancellationToken);
        return courses
            .Where(course => !lecturerId.HasValue || course.LecturerId == lecturerId)
            .Select(course => course.CourseId)
            .ToList();
    }

    private async Task EnsureCourseVisibleAsync(
        int courseId,
        int? lecturerId,
        CancellationToken cancellationToken)
    {
        if (!lecturerId.HasValue)
        {
            return;
        }

        var allowed = await VisibleCourseIdsAsync(lecturerId, cancellationToken);
        if (!allowed.Contains(courseId))
        {
            throw new BusinessValidationException("Bạn không có quyền thao tác tài liệu của môn học này.");
        }
    }

    private static CourseMaterialResponse MapMaterial(AssignmentPRN.DataAccess.Contracts.CourseMaterialItem item) => new()
    {
        MaterialId = item.MaterialId,
        CourseId = item.CourseId,
        CourseCode = item.CourseCode,
        CourseName = item.CourseName,
        FileName = item.FileName,
        FilePath = item.FilePath,
        FileType = item.FileType.ToBusiness(),
        FileSize = item.FileSize,
        UploaderName = item.UploaderName,
        UploadedAt = item.UploadedAt,
        QuestionCount = item.QuestionCount
    };
}
