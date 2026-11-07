using AssignmentPRN.Presentation.Hubs;
using Xunit;
namespace AssignmentPRN.Tests;
public class ExamHubAccessTests
{
    [Theory]
    [InlineData("Admin", 1, true)]
    [InlineData("Lecturer", 10, true)]
    [InlineData("Lecturer", 11, false)]
    [InlineData("Student", 20, true)]
    [InlineData("Student", 21, false)]
    [InlineData("Unknown", 1, false)]
    [InlineData("Admin", 0, false)]
    public void Group_membership_requires_role_and_ownership(string role, int user, bool expected) =>
        Assert.Equal(expected, ExamUpdatesHub.CanJoin(role, user, 10, [20]));

    [Theory]
    [InlineData("Admin", 1, 99, true)]
    [InlineData("Lecturer", 10, 10, true)]
    [InlineData("Lecturer", 11, 10, false)]
    [InlineData("Student", 20, 10, false)]
    [InlineData("Lecturer", 0, 0, false)]
    public void Course_group_membership_requires_admin_or_course_owner(
        string role, int user, int lecturer, bool expected) =>
        Assert.Equal(expected, ExamUpdatesHub.CanJoinCourse(role, user, lecturer));
}
