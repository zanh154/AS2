namespace AssignmentPRN.Business.Interfaces;

/// <summary>Create-or-update payloads the catalog services accept.</summary>

public record AcademicClassSaveRequest(int ClassId, string ClassCode, string ClassName,
    int CourseId, int LecturerId, bool IsActive);

public record CourseSaveRequest(int CourseId, string CourseCode, string CourseName,
    string? Description, int LecturerId, bool IsActive);
