using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Hubs;

[Authorize]
public sealed class DocumentProcessingHub : Hub
{
    public async Task JoinDocumentGroup(int documentId)
    {
        var groupName = GetDocumentGroupName(documentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveDocumentGroup(int documentId)
    {
        var groupName = GetDocumentGroupName(documentId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task JoinCourseGroup(int courseId)
    {
        var groupName = GetCourseGroupName(courseId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveCourseGroup(int courseId)
    {
        var groupName = GetCourseGroupName(courseId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var userId = httpContext?.Session.GetInt32(SessionKeys.UserId);
        
        if (!userId.HasValue)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    private static string GetDocumentGroupName(int documentId) => $"document:{documentId}";
    private static string GetCourseGroupName(int courseId) => $"course:{courseId}";

    // Static methods for server-side broadcasting
    public static string DocumentGroup(int documentId) => $"document:{documentId}";
    public static string CourseGroup(int courseId) => $"course:{courseId}";
}