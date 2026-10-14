namespace AssignmentPRN.DataAccess.Enums;

/// <summary>Mirrors the MySQL enum on questions.difficulty.</summary>
public enum QuestionDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>Mirrors the MySQL enum on questions.bloom_level.</summary>
public enum BloomLevel
{
    Remember,
    Understand,
    Apply,
    Analyze
}

/// <summary>
/// Mirrors the MySQL enum on questions.status. The bank skips the review step:
/// a question is <see cref="Approved"/> from the moment it is created, and
/// <see cref="Archived"/> only hides it without touching exam history.
/// </summary>
public enum QuestionStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Archived
}

/// <summary>Mirrors the MySQL enum on questions.question_type.</summary>
public enum QuestionType
{
    Main,
    FollowUp
}

/// <summary>Whether a person or the AI wrote the question.</summary>
public enum QuestionSource
{
    Manual,
    AI
}

/// <summary>
/// Mirrors the MySQL enum on course_materials.file_type. The members are named in
/// upper case because EF stores the member name verbatim for a string-converted enum.
/// </summary>
public enum MaterialFileType
{
    PDF,
    DOCX,
    PPTX
}

/// <summary>
/// Existing course_materials.processing_status column. Module 5 uploads a file that is
/// ready to use straight away, so it always writes Completed; the rest of the values are
/// kept so a later AI-ingestion feature can reuse the column.
/// </summary>
public enum MaterialProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>Rules the question bank applies to a question's lifecycle.</summary>
public static class QuestionRules
{
    /// <summary>Only approved questions may be handed out to an exam slot.</summary>
    public static bool IsSelectable(QuestionStatus status) => status == QuestionStatus.Approved;

    /// <summary>An AI-written question needs a lecturer's review; a manual one is trusted.</summary>
    public static bool RequiresReview(QuestionSource source) => source == QuestionSource.AI;

    /// <summary>The status a freshly created question of the given source is stored with.</summary>
    public static QuestionStatus InitialStatus(QuestionSource source) =>
        RequiresReview(source) ? QuestionStatus.PendingReview : QuestionStatus.Approved;
}
