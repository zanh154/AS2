namespace AssignmentPRN.DataAccess.Entities;

public enum DocumentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public class Document
{
    public int DocumentId { get; set; }

    public int CourseId { get; set; }

    public int UploadedById { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// The course material this is the AI index of. Every upload now goes through the
    /// material list; only rows from before the merge can still be without one.
    /// </summary>
    public int? MaterialId { get; set; }

    public CourseMaterial? Material { get; set; }

    public Course Course { get; set; } = null!;

    public User UploadedBy { get; set; } = null!;

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();

    public ICollection<GeneratedQuestion> GeneratedQuestions { get; set; } = new List<GeneratedQuestion>();
}