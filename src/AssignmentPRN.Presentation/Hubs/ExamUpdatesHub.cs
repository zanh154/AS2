using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Hubs;
public sealed class ExamUpdatesHub(IExamSessionService sessions, IAuthService auth) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var session = Context.GetHttpContext()?.Session;
        var id = session?.GetInt32(SessionKeys.UserId);
        var role = session?.GetString(SessionKeys.Role);
        var user = id is > 0 ? await auth.GetUserByIdAsync(id.Value) : null;
        if (user is null || user.RoleName != role || role is not (RoleNames.Admin or RoleNames.Lecturer or RoleNames.Student))
        { Context.Abort(); return; }
        Context.Items["userId"] = id!.Value;
        Context.Items["role"] = role;
        await Groups.AddToGroupAsync(Context.ConnectionId, ExamGroups.User(id.Value));
        if (role == RoleNames.Admin) await Groups.AddToGroupAsync(Context.ConnectionId, ExamGroups.Admins);
        await base.OnConnectedAsync();
    }
    public async Task JoinExam(int examId)
    {
        if (examId <= 0 || Context.Items["userId"] is not int id || Context.Items["role"] is not string role)
            throw new HubException("Không có quyền truy cập phiên thi.");
        var user = await auth.GetUserByIdAsync(id);
        var result = await sessions.GetAsync(examId, Context.ConnectionAborted);
        if (user is null || user.RoleName != role || result.Data is not { } exam
            || !CanJoin(role, id, exam.Lecturer.UserId, exam.Candidates.Select(x => x.StudentId)))
            throw new HubException("Không có quyền truy cập phiên thi.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ExamGroups.Exam(examId));
    }
    public Task LeaveExam(int examId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ExamGroups.Exam(examId));
    public static bool CanJoin(string role, int id, int lecturerId, IEnumerable<int> studentIds) => id > 0 &&
        (role == RoleNames.Admin || role == RoleNames.Lecturer && id == lecturerId
         || role == RoleNames.Student && studentIds.Contains(id));
}
