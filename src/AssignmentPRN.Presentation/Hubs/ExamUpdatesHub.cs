using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Hubs;

/// <summary>Push-only hub used to tell signed-in clients that a shared list changed.</summary>
public sealed class ExamUpdatesHub : Hub
{
    public override Task OnConnectedAsync()
    {
        if (!Context.GetHttpContext()!.Session.GetInt32(SessionKeys.UserId).HasValue)
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }
}
