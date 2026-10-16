namespace AssignmentPRN.Business.Interfaces;

public enum ExamSessionStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum CandidateStatus
{
    Waiting,
    InProgress,
    Completed,
    Absent,
    Cancelled
}

public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum BloomLevel
{
    Remember,
    Understand,
    Apply,
    Analyze,
    Evaluate,
    Create
}

public enum QuestionStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Archived
}

public enum QuestionType
{
    Main,
    FollowUp
}

public enum QuestionSource
{
    Manual,
    AI
}

public enum MaterialFileType
{
    PDF,
    DOCX,
    PPTX
}
