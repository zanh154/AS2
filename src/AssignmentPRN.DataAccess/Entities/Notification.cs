namespace AssignmentPRN.DataAccess.Entities;

/// <summary>
/// One message for one account, raised when something it cares about changes: a question
/// bank it draws from, or the configuration of an exam it runs. The recipient reads it
/// once and marks it read, so a badge can show what is still unseen.
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }

    /// <summary>The account that sees this message.</summary>
    public int UserId { get; set; }

    /// <summary>The course whose bank changed; null when the message is not course-scoped.</summary>
    public int? CourseId { get; set; }

    /// <summary>The exam whose configuration changed; null when the message is not exam-scoped.</summary>
    public int? ExamId { get; set; }

    /// <summary>A stable code the UI can branch on, e.g. <c>QuestionBankChanged</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The text shown to the recipient.</summary>
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
