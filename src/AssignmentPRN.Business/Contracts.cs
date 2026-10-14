using AssignmentPRN.DataAccess.Enums;

namespace AssignmentPRN.Business;

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
            Error = values.FirstOrDefault() ?? "Không thể hoàn tất thao tác.",
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
            Error = values.FirstOrDefault() ?? "Không thể hoàn tất thao tác.",
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
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; }

    public DateTime EndTime { get; init; }

    public int TimePerStudent { get; init; }

    public ExamSessionStatus Status { get; init; }

    public string LecturerName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

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

public sealed class CourseResponse
{
    public int CourseId { get; init; }

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string? Description { get; init; }
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

    public int StudentCount { get; init; }
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
    public int ExamId { get; init; }

    public string ExamName { get; init; } = string.Empty;

    public string CourseCode { get; init; } = string.Empty;

    public string CourseName { get; init; } = string.Empty;

    public string LecturerName { get; init; } = string.Empty;

    public int LecturerId { get; init; }

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
        : base(errors.FirstOrDefault() ?? "Dữ liệu không hợp lệ.")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}
