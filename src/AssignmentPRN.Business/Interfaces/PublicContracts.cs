namespace AssignmentPRN.Business.Interfaces;

public sealed class ExamSessionUpdateInput
{
    public int ExamId { get; set; }
    public int CourseId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public int TimePerStudent { get; set; }
    public int MainQuestionCount { get; set; }
    public int MaxFollowUpCount { get; set; }
}

public sealed class ExamStudentSearch
{
    public string? Query { get; set; }
    public int? ExamId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public CandidateStatus? Status { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record ExamStudentRow(
    int CandidateId,
    int ExamId,
    string StudentName,
    string Email,
    string ExamName,
    string CourseName,
    DateTime? ScheduledTime,
    CandidateStatus Status);

public sealed record ExamStudentSessionOption(int ExamId, string ExamName);

public sealed class ExamStudentSearchResult
{
    public IReadOnlyList<ExamStudentRow> Items { get; init; } = [];
    public IReadOnlyList<ExamStudentSessionOption> Sessions { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize => 20;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed class QuestionOptionInput
{
    public string Text { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }
}

public sealed class ExamQuestionAssignmentRequest
{
    public int ExamId { get; init; }
    public int CourseId { get; init; }
    public int CountPerCandidate { get; init; }
    public IReadOnlyList<int> MaterialIds { get; init; } = [];
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = [];
}

public sealed class ExamQuestionAssignmentResult
{
    public int ExamId { get; init; }
    public int CandidateCount { get; init; }
    public int AssignedCount { get; init; }
    public IReadOnlyList<int> TakenQuestionIds { get; init; } = [];
}

public sealed class QuestionPickRequest
{
    public int CourseId { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<int> MaterialIds { get; init; } = [];
    public IReadOnlyList<QuestionDifficulty> Difficulties { get; init; } = [];
    public IReadOnlyCollection<int> TakenQuestionIds { get; init; } = [];
}

public sealed class ExamRoomQuestion
{
    public int ExamQuestionId { get; init; }
    public int OrderNo { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionDifficulty Difficulty { get; init; }
    public IReadOnlyList<ExamRoomOption> Options { get; init; } = [];
    public int? SelectedOptionId { get; init; }
    public int? ParentExamQuestionId { get; init; }
    public bool IsFollowUp => ParentExamQuestionId.HasValue;
}

public sealed class ExamRoomOption
{
    public int OptionId { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class ExamResultQuestion
{
    public int ExamQuestionId { get; init; }
    public int OrderNo { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionDifficulty Difficulty { get; init; }
    public IReadOnlyList<ExamResultOption> Options { get; init; } = [];
    public int? SelectedOptionId { get; init; }
    public int? MaterialId { get; init; }
    public int? ParentExamQuestionId { get; init; }
    public bool IsFollowUp => ParentExamQuestionId.HasValue;
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

public sealed class ExamPaperItem
{
    public int CandidateId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudentEmail { get; init; } = string.Empty;
    public DateTime? ScheduledTime { get; init; }
    public bool HasStarted { get; init; }
    public IReadOnlyList<ExamPaperQuestion> Questions { get; init; } = [];
}

public sealed class ExamPaperQuestion
{
    public int ExamQuestionId { get; init; }
    public int QuestionId { get; init; }
    public int OrderNo { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionDifficulty Difficulty { get; init; }
    public string? MaterialName { get; init; }
    public bool IsCompleted { get; init; }
}

public sealed class MaterialDownload
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public MaterialFileType FileType { get; init; }
}
