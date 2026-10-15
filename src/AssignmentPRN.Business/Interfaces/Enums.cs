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
    Analyze
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

public enum DocumentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum DocumentBloomLevel
{
    Remember = 1,
    Understand = 2,
    Apply = 3,
    Analyze = 4,
    Evaluate = 5,
    Create = 6
}

public enum GeneratedQuestionStatus
{
    PendingReview = 0,
    Approved = 1,
    Rejected = 2
}
