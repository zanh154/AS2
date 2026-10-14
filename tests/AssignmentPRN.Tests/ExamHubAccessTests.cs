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
}
