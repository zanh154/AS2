using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Hubs;
public sealed class DocumentProcessingHub(IDocumentService documents, ICatalogService catalog, IAuthService auth) : Hub
{
    public override async Task OnConnectedAsync() {
        var session = Context.GetHttpContext()?.Session;
        var id = session?.GetInt32(SessionKeys.UserId);
        var user = id is > 0 ? await auth.GetUserByIdAsync(id.Value) : null;
        if (user is null || user.RoleName != session?.GetString(SessionKeys.Role) || user.RoleName is not (RoleNames.Admin or RoleNames.Lecturer)) {
            Context.Abort(); return;
        }
        Context.Items["userId"] = user.UserId;
        Context.Items["role"] = user.RoleName;
        await base.OnConnectedAsync();
    }
    public async Task JoinDocumentGroup(int documentId) {
        var result = await documents.GetAsync(documentId, Context.ConnectionAborted);
        if (result.Data is null || !await CanAccessCourse(result.Data.CourseId)) throw new HubException("Không có quyền truy cập tài liệu.");
        await Groups.AddToGroupAsync(Context.ConnectionId, DocumentGroup(documentId));
    }
    public async Task JoinCourseGroup(int courseId) {
        if (!await CanAccessCourse(courseId)) throw new HubException("Không có quyền truy cập môn học.");
        await Groups.AddToGroupAsync(Context.ConnectionId, CourseGroup(courseId));
    }
    public Task LeaveDocumentGroup(int id) => Groups.RemoveFromGroupAsync(Context.ConnectionId, DocumentGroup(id));
    public Task LeaveCourseGroup(int id) => Groups.RemoveFromGroupAsync(Context.ConnectionId, CourseGroup(id));
    private async Task<bool> CanAccessCourse(int courseId) {
        if (Context.Items["userId"] is not int id || Context.Items["role"] is not string role) return false;
        var user = await auth.GetUserByIdAsync(id);
        if (user is null || user.RoleName != role) return false;
        if (role == RoleNames.Admin) return true;
        var courses = await catalog.ListActiveCoursesAsync(Context.ConnectionAborted);
        return role == RoleNames.Lecturer && courses.Data?.Any(x => x.CourseId == courseId && x.LecturerId == id) == true;
    }
    public static string DocumentGroup(int id) => $"document:{id}";
    public static string CourseGroup(int id) => $"course:{id}";
}
