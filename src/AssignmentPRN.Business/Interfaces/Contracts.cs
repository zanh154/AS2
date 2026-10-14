using AssignmentPRN.Business.BusinessRules;

namespace AssignmentPRN.Business.Interfaces;

/// <summary>Result of an operation that returns nothing but can fail with user-facing messages.</summary>
public sealed class ServiceResponse
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ServiceResponse Ok() => new() { Success = true };

    public static ServiceResponse Fail(string error) => Fail([error]);

    public static ServiceResponse Fail(IEnumerable<string> errors)
    {
        var values = errors.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

        return new ServiceResponse
        {
            Success = false,
            Error = values.FirstOrDefault() ?? "KhÃ´ng thá»ƒ hoÃ n táº¥t thao tÃ¡c.",
            Errors = values
        };
    }
}

public sealed class ServiceResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ServiceResponse<T> Ok(T data) => new() { Success = true, Data = data };

    public static ServiceResponse<T> Fail(string error) => Fail([error]);

    public static ServiceResponse<T> Fail(IEnumerable<string> errors)
    {
        var values = errors.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

        return new ServiceResponse<T>
        {
            Success = false,
            Error = values.FirstOrDefault() ?? "KhÃ´ng thá»ƒ hoÃ n táº¥t thao tÃ¡c.",
            Errors = values
        };
    }
}

public sealed class ExamSessionCreateRequest
{
    public int CourseId { get; init; }

    public int LecturerId { get; init; }

    public int ClassId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>When the first student starts.</summary>
    public DateTime StartTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

}

public sealed class ExamSessionRescheduleRequest
{
    public int CandidateId { get; init; }

    public DateTime ScheduledTime { get; init; }
}

public sealed class ExamSessionListItemResponse
{
    public int LecturerId { get; init; }
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public ExamSessionStatus Status { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int CandidateCount { get; init; }
}

public sealed class ExamSessionDetailResponse
{
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

    public ExamSessionStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public PersonResponse Lecturer { get; init; } = new();

    public CourseResponse Course { get; init; } = new();

    public IReadOnlyList<ExamCandidateResponse> Candidates { get; init; } = Array.Empty<ExamCandidateResponse>();
}

public sealed class PersonResponse
{
    public int UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}

public sealed class AuthenticatedUserResponse
{
    public int UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;
}

public sealed class CourseResponse
{
    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int LecturerId { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class AcademicClassResponse
{
    public int ClassId { get; init; }

    public string ClassCode { get; init; } = string.Empty;

    public string ClassName { get; init; } = string.Empty;

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int StudentCount { get; init; }
}

public sealed class AcademicClassRosterResponse
{
    public AcademicClassResponse Class { get; init; } = new();

    public IReadOnlyList<ClassStudentResponse> Students { get; init; } = Array.Empty<ClassStudentResponse>();
}

public sealed class ClassStudentResponse
{
    public int StudentId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public DateTime JoinedAt { get; init; }
}

public sealed class ExamCandidateResponse
{
    public int CandidateId { get; init; }

    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public DateTime? ScheduledTime { get; init; }

    public DateTime? EndTime { get; init; }

    public CandidateStatus Status { get; init; }
}

public sealed class ExamSessionOptionsResponse
{
    public IReadOnlyList<LookupOption> Courses { get; init; } = Array.Empty<LookupOption>();

    public IReadOnlyList<LookupOption> Lecturers { get; init; } = Array.Empty<LookupOption>();

    public IReadOnlyList<ClassOptionResponse> Classes { get; init; } = Array.Empty<ClassOptionResponse>();
}

public sealed class ClassOptionResponse
{
    public int ClassId { get; init; }

    public int CourseId { get; init; }

    public int LecturerId { get; init; }

    public string Label { get; init; } = string.Empty;

    public int StudentCount { get; init; }
}

public sealed class ClassRosterResponse
{
    public int ClassId { get; init; }

    public int CourseId { get; init; }

    public int LecturerId { get; init; }

    public string ClassCode { get; init; } = string.Empty;

    public string ClassName { get; init; } = string.Empty;

    public IReadOnlyList<PersonResponse> Students { get; init; } = Array.Empty<PersonResponse>();
}

/// <summary>A single choice in a dropdown: the identifier plus the label shown to the user.</summary>
public sealed record LookupOption(int Id, string Label);

public sealed class StudentScheduleResponse
{
    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public IReadOnlyList<StudentScheduleItemResponse> Items { get; init; } =
        Array.Empty<StudentScheduleItemResponse>();
}

public sealed class StudentScheduleItemResponse
{
    public int LecturerId { get; init; }
    public int ExamId { get; init; }

    /// <summary>The student's own slot; what the exam room is opened by.</summary>
    public int CandidateId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public DateTime ScheduledTime { get; init; }

    public DateTime EndTime { get; init; }

    public ExamSessionStatus SessionStatus { get; init; }

    public CandidateStatus CandidateStatus { get; init; }
}

/// <summary>Carries one or more messages that are safe to show directly to the user.</summary>
public sealed class BusinessValidationException : Exception
{
    public BusinessValidationException(string error)
        : base(error)
    {
        Errors = [error];
    }

    public BusinessValidationException(IEnumerable<string> errors)
        : base(errors.FirstOrDefault() ?? "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡.")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class QuestionResponse
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string? MaterialName { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public QuestionType QuestionType { get; init; }

    public QuestionStatus Status { get; init; }

    public IReadOnlyList<QuestionOptionResponse> Options { get; init; } = Array.Empty<QuestionOptionResponse>();
}

public sealed class QuestionOptionResponse
{
    public string Text { get; init; } = string.Empty;

    public bool IsCorrect { get; init; }
}

public sealed class CourseMaterialResponse
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

    public int QuestionCount { get; init; }
}

/// <summary>
/// How a question should be saved from the editor: <c>Options</c> holds the answer
/// choices in display order and exactly one of them must be marked correct.
/// </summary>
public sealed class QuestionSaveRequest
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public int? MaterialId { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public QuestionType QuestionType { get; init; } = QuestionType.Main;

    public IReadOnlyList<QuestionOptionInput> Options { get; init; } = [];
}

/// <summary>One row of the question bank list.</summary>
public sealed class QuestionListItemResponse
{
    public int QuestionId { get; init; }

    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public int? MaterialId { get; init; }

    public string? MaterialName { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public QuestionDifficulty Difficulty { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionType QuestionType { get; init; }

    public QuestionStatus Status { get; init; }

    public string AuthorName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// What a student sees after opening their slot: the session they are sitting and the
/// paper the bank dealt them. It carries no answer key.
/// </summary>
public sealed class ExamRoomResponse
{
    public int CandidateId { get; init; }

    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public DateTime ScheduledTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public CandidateStatus CandidateStatus { get; init; }

    public DateTime? StartedAt { get; init; }

    public IReadOnlyList<ExamRoomQuestion> Questions { get; init; } = [];

    /// <summary>False once the paper has been handed in; the room then only shows it.</summary>
    public bool CanAnswer { get; init; }

    /// <summary>
    /// Seconds left in the slot when the page was rendered. The countdown starts from this
    /// rather than from the browser's clock, which may be minutes out.
    /// </summary>
    public int SecondsRemaining { get; init; }

    /// <summary>
    /// True once the main round is handed in and follow-ups were dealt. Only the follow-ups
    /// can then be answered; the main round is shown as it was submitted.
    /// </summary>
    public bool IsFollowUpRound => Questions.Any(question => question.IsFollowUp);

    /// <summary>The questions the student is working on right now.</summary>
    public IReadOnlyList<ExamRoomQuestion> OpenRound =>
        Questions.Where(question => question.IsFollowUp == IsFollowUpRound).ToList();

    /// <summary>The main round, already handed in, while the follow-ups are being answered.</summary>
    public IReadOnlyList<ExamRoomQuestion> SubmittedRound => IsFollowUpRound
        ? Questions.Where(question => !question.IsFollowUp).ToList()
        : [];

    public int AnsweredCount => Questions.Count(question => question.SelectedOptionId.HasValue);
}

/// <summary>A completed paper together with its grade and answer review.</summary>
public sealed class ExamResultResponse
{
    public int CandidateId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public IReadOnlyList<ExamResultQuestion> Questions { get; init; } = [];

    public int TotalQuestions => Questions.Count;

    public int CorrectCount => Questions.Count(question => question.IsCorrect);

    public int UnansweredCount => Questions.Count(question => !question.SelectedOptionId.HasValue);

    public int IncorrectCount => TotalQuestions - CorrectCount - UnansweredCount;

    public IReadOnlyList<ExamResultQuestion> MainQuestions =>
        Questions.Where(question => !question.IsFollowUp).ToList();

    public IReadOnlyList<ExamResultQuestion> FollowUpQuestions =>
        Questions.Where(question => question.IsFollowUp).ToList();

    public int FollowUpCorrectCount => Questions.Count(question => question.IsFollowUp && question.IsCorrect);

    /// <summary>Out of 10; a follow-up weighs half a main question, see <see cref="ExamScoring"/>.</summary>
    public decimal Score => ExamScoring.Score(
        Questions.Select(question => (question.IsFollowUp, question.IsCorrect)));
}

/// <summary>How many questions the bank can still hand out for one exam.</summary>
public sealed class QuestionAvailabilityResponse
{
    public int Requested { get; init; }

    public int Available { get; init; }

    public int Missing => Math.Max(0, Requested - Available);

    public bool IsEnough => Missing == 0;
}

/// <summary>A row of the CSV import, already trimmed and free of empty lines.</summary>
public sealed class QuestionImportRow
{
    /// <summary>Line in the source file, so a later failure can point back at it.</summary>
    public int SourceLine { get; init; }

    public string QuestionText { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public int? MaterialId { get; init; }

    public BloomLevel BloomLevel { get; init; }

    public QuestionDifficulty Difficulty { get; init; }

    public IReadOnlyList<QuestionOptionInput> Options { get; init; } = [];
}

public sealed class QuestionImportResult
{
    public int Imported { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool Success => Errors.Count == 0;
}

/// <summary>A session's score sheet as the examiner reads it.</summary>
public sealed class SessionResultResponse
{
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public int TimePerStudent { get; init; }

    public int MainQuestionCount { get; init; }

    public int MaxFollowUpCount { get; init; }

    public ExamSessionStatus Status { get; init; }

    public IReadOnlyList<CandidateResultItemResponse> Candidates { get; init; } =
        Array.Empty<CandidateResultItemResponse>();

    /// <summary>Slots whose time is over but are still Waiting or In progress.</summary>
    public int OverdueCount { get; init; }

    public int CompletedCount => Candidates.Count(item => item.Status == CandidateStatus.Completed);

    public int AbsentCount => Candidates.Count(item => item.Status == CandidateStatus.Absent);

    /// <summary>Mean score of the completed papers; null while none is completed.</summary>
    public decimal? AverageScore => CompletedCount == 0
        ? null
        : Math.Round(
            Candidates.Where(item => item.Score.HasValue).Average(item => item.Score!.Value),
            1,
            MidpointRounding.AwayFromZero);
}

/// <summary>One row of the score sheet.</summary>
public sealed class CandidateResultItemResponse
{
    public int CandidateId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public DateTime? ScheduledTime { get; init; }

    public CandidateStatus Status { get; init; }

    public int MainCorrect { get; init; }

    public int MainTotal { get; init; }

    public int FollowUpCorrect { get; init; }

    public int FollowUpTotal { get; init; }

    /// <summary>Out of 10, only once the paper is completed.</summary>
    public decimal? Score { get; init; }
}

/// <summary>One candidate's marked paper, for the examiner.</summary>
public sealed class CandidateResultResponse
{
    public int ExamId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string StudentEmail { get; init; } = string.Empty;

    public CandidateStatus Status { get; init; }

    public DateTime? ScheduledTime { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    /// <summary>The paper, graded; its score only means something once the slot is completed.</summary>
    public ExamResultResponse Paper { get; init; } = new();
}

/// <summary>How many slots a close-overdue pass moved.</summary>
public sealed class OverdueSlotsResult
{
    public int MarkedAbsent { get; init; }

    public int Completed { get; init; }
}
