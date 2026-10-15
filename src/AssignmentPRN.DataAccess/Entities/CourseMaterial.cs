using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// A file uploaded for a course. A material doubles as the "topic" a question is
/// filed under: the bank groups and filters questions by <see cref="MaterialId"/>.
/// </summary>
public class CourseMaterial
{
    public int MaterialId { get; set; }

    public int CourseId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Web-relative path of the stored file, e.g. /materials/prn212_week2.pdf.</summary>
    public string FilePath { get; set; } = string.Empty;

    public MaterialFileType FileType { get; set; }

    public long? FileSize { get; set; }

    public int UploadedBy { get; set; }

    /// <summary>
    /// Always <see cref="MaterialProcessingStatus.Completed"/> for an upload made through
    /// the UI: the file is usable as a topic immediately, with no processing step.
    /// </summary>
    public MaterialProcessingStatus ProcessingStatus { get; set; }

    public DateTime UploadedAt { get; set; }

    public Course Course { get; set; } = null!;

    public User Uploader { get; set; } = null!;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
