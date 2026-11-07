using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.DataAccess.Contracts;

/// <summary>Filter applied when browsing the bank.</summary>
public sealed class QuestionQuery
{
    /// <summary>Restrict to one course. Null browses every course the caller may see.</summary>
    public int? CourseId { get; init; }

    /// <summary>Restrict to one topic, i.e. one course material.</summary>
    public int? MaterialId { get; init; }

    /// <summary>Restrict to one difficulty. Null includes every difficulty.</summary>
    public QuestionDifficulty? Difficulty { get; init; }

    /// <summary>Restrict to one Bloom level. Null includes every level.</summary>
    public BloomLevel? BloomLevel { get; init; }

    /// <summary>Restrict to one status. Null includes every status.</summary>
    public QuestionStatus? Status { get; init; }

    /// <summary>Free-text match against the question body and the expected answer.</summary>
    public string? Term { get; init; }

    /// <summary>Include archived questions. They are hidden by default.</summary>
    public bool IncludeArchived { get; init; }
}

/// <summary>Everything needed to write a question: the body, its metadata and its choices.</summary>
public sealed class QuestionUpsertInput
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    /// <summary>Main questions go out in the first round; follow-ups only after it.</summary>
    public QuestionType QuestionType { get; init; } = QuestionType.Main;

    /// <summary>Who wrote the question. AI-generated questions are held for review.</summary>
    public QuestionSource Source { get; init; } = QuestionSource.Manual;

    public IReadOnlyList<QuestionOptionInput> Options { get; init; } = Array.Empty<QuestionOptionInput>();
}

public sealed class QuestionOptionInput
{
    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }
}

/// <summary>
/// One exam slot for one candidate. Written when the exam is created, then read and
/// updated by the results module while the student answers.
/// </summary>
public sealed class ExamQuestionInput
{
    public int CandidateId { get; init; }

    public int QuestionId { get; init; }

    /// <summary>1-based position inside the candidate's own paper.</summary>
    public int OrderNo { get; init; }

    /// <summary>The main slot a follow-up digs into; null for a main question.</summary>
    public int? ParentExamQuestionId { get; init; }
}

/// <summary>A follow-up question of the bank, with what the planner matches it on.</summary>
public sealed class FollowUpPoolItem
{
    public int QuestionId { get; init; }

    public int MaterialId { get; init; }

    public QuestionDifficulty Difficulty { get; init; }
}

/// <summary>
/// Everything the bank needs to deal a set of questions to a freshly created exam.
/// The candidates are read from the exam itself, so the caller does not have to repeat
/// the roster and cannot disagree with it.
/// </summary>
public sealed class ExamQuestionAssignmentRequest
{
    public int ExamId { get; init; }

    public int CourseId { get; init; }

    /// <summary>How many questions each candidate receives.</summary>
    public int CountPerCandidate { get; init; }

    /// <summary>Restrict the pool to these topics. Empty means every topic of the course.</summary>
    public IReadOnlyList<int> MaterialIds { get; init; } = Array.Empty<int>();

    /// <summary>Restrict the pool to these difficulties. Empty means every difficulty.</summary>
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = Array.Empty<QuestionDifficulty>();
}

public sealed class ExamQuestionAssignmentResult
{
    public int ExamId { get; init; }

    public int CandidateCount { get; init; }

    /// <summary>Number of exam_questions rows written.</summary>
    public int AssignedCount { get; init; }

    /// <summary>Questions now in use by the exam; the pool must avoid these next time.</summary>
    public IReadOnlyList<int> TakenQuestionIds { get; init; } = Array.Empty<int>();
}

/// <summary>
/// The bank hands questions out per exam slot. <see cref="TakenQuestionIds"/> are the
/// questions already assigned to the exam being built, so a question is never repeated
/// between two students sitting the same session.
/// </summary>
public sealed class QuestionPickRequest
{
    public int CourseId { get; init; }

    /// <summary>How many questions each student receives.</summary>
    public int Count { get; init; }

    /// <summary>Restrict the pool to these topics. Empty means every topic of the course.</summary>
    public IReadOnlyList<int> MaterialIds { get; init; } = Array.Empty<int>();

    /// <summary>Restrict the pool to these difficulties. Empty means every difficulty.</summary>
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = Array.Empty<QuestionDifficulty>();

    /// <summary>Questions already given out in this exam; they are excluded from the pool.</summary>
    public IReadOnlyCollection<int> TakenQuestionIds { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Result of asking whether an exam can be built: how many questions the bank can
/// supply and how many are still missing, so the lecturer is warned before the exam
/// is created rather than at randomisation time.
/// </summary>
public sealed class QuestionAvailability
{
    public int Requested { get; init; }

    /// <summary>Approved, not-yet-taken questions currently in the pool.</summary>
    public int Available { get; init; }

    public int Missing => Math.Max(0, Requested - Available);

    public bool IsEnough => Missing == 0;
}

/// <summary>One row of the question list.</summary>
public sealed class QuestionListItem
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int? MaterialId { get; init; }

    public string? MaterialName { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionType QuestionType { get; init; }

    public QuestionStatus Status { get; init; }

    /// <summary>Who wrote the question. AI-generated ones are held for review.</summary>
    public QuestionSource Source { get; init; }

    public string AuthorName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

/// <summary>A question with its choices, used by the edit screen and the examiner preview.</summary>
public sealed class QuestionDetail
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public QuestionType QuestionType { get; init; }

    public QuestionStatus Status { get; init; }

    /// <summary>Who wrote the question. AI-generated ones are held for review.</summary>
    public QuestionSource Source { get; init; }

    /// <summary>The reviewer who approved or rejected it; null while it is still pending.</summary>
    public int? ReviewedBy { get; init; }

    /// <summary>When the review decision was made; null while it is still pending.</summary>
    public DateTime? ReviewedAt { get; init; }

    public IReadOnlyList<QuestionOptionDetail> Options { get; init; } = Array.Empty<QuestionOptionDetail>();
}

/// <summary>
/// Lightweight row used only to spot a question that repeats one already in the course.
/// Archived questions are left out, because retiring a question and re-adding an
/// improved version of it is a legitimate thing to do.
/// </summary>
public sealed class QuestionTextMatch
{
    public int QuestionId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionStatus Status { get; init; }

    public int? MaterialId { get; init; }
}

public sealed class QuestionOptionDetail
{
    public int OptionId { get; init; }
    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }

    public int DisplayOrder { get; init; }
}

/// <summary>One row of the course material list.</summary>
public sealed class CourseMaterialItem
{
    public int MaterialId { get; init; }

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public MaterialFileType FileType { get; init; }

    public long? FileSize { get; init; }

    public string UploaderName { get; init; } = string.Empty;

    public DateTime UploadedAt { get; init; }

    /// <summary>How many questions are filed under this topic.</summary>
    public int QuestionCount { get; init; }

    public int ManualQuestionCount { get; init; }

    public int AiQuestionCount { get; init; }

    /// <summary>The AI index of this file, when there is one.</summary>
    public int? DocumentId { get; init; }

    public Entities.DocumentStatus? AiStatus { get; init; }

    public int GeneratedQuestionCount { get; init; }

    public int PendingGeneratedQuestionCount { get; init; }

    public int ApprovedGeneratedQuestionCount { get; init; }

    public int RejectedGeneratedQuestionCount { get; init; }
}

/// <summary>
/// Everything the exam room needs to decide whether a student may sit their slot, and
/// to deal them a paper if they do not have one yet.
/// </summary>
public sealed class ExamRoomCandidate
{
    public int CandidateId { get; init; }

    public int ExamId { get; init; }

    /// <summary>Account the slot belongs to; only this student may open it.</summary>
    public int StudentId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    /// <summary>Null while the session has not been scheduled; such a slot cannot be sat.</summary>
    public DateTime? ScheduledTime { get; init; }

    public int TimePerStudent { get; init; }

    /// <summary>How many questions this session gives each student.</summary>
    public int MainQuestionCount { get; init; }

    /// <summary>Cap on follow-ups this session may ask one student after the main round.</summary>
    public int MaxFollowUpCount { get; init; }

    public CandidateStatus CandidateStatus { get; init; }

    public ExamSessionStatus SessionStatus { get; init; }

    public DateTime? StartedAt { get; init; }
}

/// <summary>
/// One question as the student sees it. There is deliberately no "is correct" here: the
/// exam room reads this type, so the answer key cannot reach the student by accident.
/// </summary>
public sealed class ExamRoomQuestion
{
    /// <summary>The slot this question occupies; an answer is saved against it.</summary>
    public int ExamQuestionId { get; init; }

    public int OrderNo { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public IReadOnlyList<ExamRoomOption> Options { get; init; } = Array.Empty<ExamRoomOption>();

    /// <summary>The choice the student has ticked, or null while the question is unanswered.</summary>
    public int? SelectedOptionId { get; init; }

    /// <summary>The main slot this follow-up digs into; null for a main question.</summary>
    public int? ParentExamQuestionId { get; init; }

    public bool IsFollowUp => ParentExamQuestionId.HasValue;
}

/// <summary>One choice on the student's paper. Carries no "is correct" for the same reason.</summary>
public sealed class ExamRoomOption
{
    public int OptionId { get; init; }

    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// One graded question returned only after the candidate has completed the exam.
/// Unlike <see cref="ExamRoomQuestion"/>, this type intentionally carries the answer key.
/// </summary>
public sealed class ExamResultQuestion
{
    public int ExamQuestionId { get; init; }

    public int OrderNo { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public IReadOnlyList<ExamResultOption> Options { get; init; } = Array.Empty<ExamResultOption>();

    public int? SelectedOptionId { get; init; }

    /// <summary>Topic of the question; follow-ups are matched on it.</summary>
    public int? MaterialId { get; init; }

    /// <summary>The main slot this follow-up digs into; null for a main question.</summary>
    public int? ParentExamQuestionId { get; init; }

    public bool IsFollowUp => ParentExamQuestionId.HasValue;

    /// <summary>Examiner notes on the question; filled only for the lecturer's review.</summary>
    public string? ExpectedAnswer { get; init; }

    public bool IsCorrect => SelectedOptionId is int selected
        && Options.Any(option => option.OptionId == selected && option.IsCorrect);
}

public sealed class ExamResultOption
{
    public int OptionId { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }
}

/// <summary>
/// One candidate's paper, read back after the bank has dealt the exam. The exam screen
/// shows it so the lecturer can see exactly what each student will be asked.
/// </summary>
public sealed class ExamPaperItem
{
    public int CandidateId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public DateTime? ScheduledTime { get; init; }

    /// <summary>The student has opened their slot, so their paper is part of the exam record.</summary>
    public bool HasStarted { get; init; }

    public IReadOnlyList<ExamPaperQuestion> Questions { get; init; } = Array.Empty<ExamPaperQuestion>();
}

/// <summary>What an exam has dealt so far, read while the exam is locked for dealing.</summary>
public sealed class ExamDealState
{
    public QuestionPickRequest Configuration { get; init; } = new();

    public IReadOnlySet<int> TakenQuestionIds { get; init; } = new HashSet<int>();

    public IReadOnlySet<int> CandidatesWithPaper { get; init; } = new HashSet<int>();
}

public sealed class ExamPaperQuestion
{
    public int ExamQuestionId { get; init; }

    public int QuestionId { get; init; }

    /// <summary>1-based position inside the candidate's own paper.</summary>
    public int OrderNo { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public string? MaterialName { get; init; }

    /// <summary>True once the student has answered it; such a paper may no longer be redealt.</summary>
    public bool IsCompleted { get; init; }
}

/// <summary>One row of the rubric list.</summary>
public sealed class RubricListItem
{
    public int RubricId { get; init; }
    public int QuestionId { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public int CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public decimal TotalScore { get; init; }
    public int CriteriaCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>A rubric with its criteria, used by the edit screen.</summary>
public sealed class RubricDetail
{
    public int RubricId { get; init; }
    public int QuestionId { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public decimal TotalScore { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<RubricCriteriaDetail> Criteria { get; init; } = Array.Empty<RubricCriteriaDetail>();
}

/// <summary>One criterion in a rubric.</summary>
public sealed class RubricCriteriaDetail
{
    public int CriteriaId { get; init; }
    public string CriteriaName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MaxScore { get; init; }
    public int DisplayOrder { get; init; }
}

/// <summary>Input for creating or updating a rubric with its criteria.</summary>
public sealed class RubricUpsertInput
{
    public int RubricId { get; init; }
    public int QuestionId { get; init; }
    public IReadOnlyList<RubricCriteriaInput> Criteria { get; init; } = Array.Empty<RubricCriteriaInput>();
}

/// <summary>One criterion input for creating or updating a rubric.</summary>
public sealed class RubricCriteriaInput
{
    public int CriteriaId { get; init; }
    public string CriteriaName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MaxScore { get; init; }
    public int DisplayOrder { get; init; }
}
