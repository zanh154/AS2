using AssignmentPRN.Presentation.Constants;
using AssignmentPRN.Presentation.Security;

namespace AssignmentPRN.Tests;

public sealed class RoleAccessRulesTests
{
    private const int CurrentUser = 10;
    private const int OtherLecturer = 11;

    [Theory]
    [InlineData(RoleNames.Admin, CurrentUser, OtherLecturer, true)]
    [InlineData(RoleNames.Lecturer, CurrentUser, CurrentUser, true)]
    [InlineData(RoleNames.Lecturer, CurrentUser, OtherLecturer, false)]
    [InlineData(RoleNames.Student, CurrentUser, CurrentUser, false)]
    [InlineData(null, CurrentUser, CurrentUser, false)]
    public void CanManageExam_EnforcesRoleAndOwnership(
        string? role,
        int userId,
        int lecturerId,
        bool expected)
    {
        Assert.Equal(expected, RoleAccessRules.CanManageExam(role, userId, lecturerId));
    }

    [Theory]
    [InlineData(RoleNames.Admin, CurrentUser, OtherLecturer, true)]
    [InlineData(RoleNames.Lecturer, CurrentUser, CurrentUser, true)]
    [InlineData(RoleNames.Lecturer, CurrentUser, OtherLecturer, false)]
    [InlineData(RoleNames.Student, CurrentUser, CurrentUser, false)]
    public void CanManageClass_EnforcesRoleAndOwnership(
        string role,
        int userId,
        int lecturerId,
        bool expected)
    {
        Assert.Equal(expected, RoleAccessRules.CanManageClass(role, userId, lecturerId));
    }

    [Theory]
    [InlineData(RoleNames.Admin, true)]
    [InlineData(RoleNames.Lecturer, true)]
    [InlineData(RoleNames.Student, false)]
    public void CanSearchStudents_IsLimitedToStaff(string role, bool expected)
    {
        Assert.Equal(expected, RoleAccessRules.CanSearchStudents(role));
    }

    [Theory]
    [InlineData(RoleNames.Admin, CurrentUser, 99, true)]
    [InlineData(RoleNames.Lecturer, CurrentUser, 99, true)]
    [InlineData(RoleNames.Student, CurrentUser, CurrentUser, true)]
    [InlineData(RoleNames.Student, CurrentUser, 99, false)]
    public void CanViewStudentSchedule_StudentsCanOnlyViewTheirOwn(
        string role,
        int userId,
        int requestedStudentId,
        bool expected)
    {
        Assert.Equal(
            expected,
            RoleAccessRules.CanViewStudentSchedule(role, userId, requestedStudentId));
    }
}
