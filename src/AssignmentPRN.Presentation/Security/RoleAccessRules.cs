using AssignmentPRN.Presentation.Constants;

namespace AssignmentPRN.Presentation.Security;

/// <summary>
/// The application's role matrix in one place. Page models still validate ownership
/// because knowing a role alone is not enough to protect a lecturer's records.
/// </summary>
public static class RoleAccessRules
{
    public static bool CanManageExam(string? role, int userId, int lecturerId) =>
        role == RoleNames.Admin || (role == RoleNames.Lecturer && userId == lecturerId);

    public static bool CanManageClass(string? role, int userId, int lecturerId) =>
        role == RoleNames.Admin || (role == RoleNames.Lecturer && userId == lecturerId);

    public static bool CanSearchStudents(string? role) =>
        role is RoleNames.Admin or RoleNames.Lecturer;

    public static bool CanViewStudentSchedule(string? role, int userId, int requestedStudentId) =>
        role is RoleNames.Admin or RoleNames.Lecturer
        || (role == RoleNames.Student && userId == requestedStudentId);
}
