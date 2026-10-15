using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Entities;

/// <summary>One exam sitting: a course, a lecturer, a window, and the students queued inside it.</summary>
public class ExamSession
{
    public int ExamId { get; set; }

    public int CourseId { get; set; }

    /// <summary>The account running the exam; a user with the Lecturer role.</summary>
    public int LecturerId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>When the first student starts.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>When the last student finishes; derived from the generated slots.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Minutes allotted to every student in this session.</summary>
    public int TimePerStudent { get; set; }

    public int MainQuestionCount { get; set; }

    /// <summary>Persisted topic/difficulty selection shared by all main-paper draws.</summary>
    public string? QuestionScopeJson { get; set; }

    public int MaxFollowUpCount { get; set; }

    public ExamSessionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;

    public User Lecturer { get; set; } = null!;

    public ICollection<ExamCandidate> Candidates { get; set; } = new List<ExamCandidate>();
}
