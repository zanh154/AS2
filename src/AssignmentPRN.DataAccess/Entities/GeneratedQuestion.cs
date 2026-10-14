namespace AssignmentPRN.DataAccess.Entities;

public enum QuestionStatus
{
    PendingReview = 0,
    Approved = 1,
    Rejected = 2
}

public enum BloomLevel
{
    Remember = 1,
    Understand = 2,
    Apply = 3,
    Analyze = 4,
    Evaluate = 5,
    Create = 6
}

public class GeneratedQuestion
{
    public int QuestionId { get; set; }

    public int DocumentId { get; set; }

    public int? CourseId { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public string? AnswerKey { get; set; }

    public string? Explanation { get; set; }

    public BloomLevel BloomLevel { get; set; }

    public string? Topic { get; set; }

    public QuestionStatus Status { get; set; } = QuestionStatus.PendingReview;

    public int? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;

    public Course? Course { get; set; }

    public User? ReviewedBy { get; set; }
}