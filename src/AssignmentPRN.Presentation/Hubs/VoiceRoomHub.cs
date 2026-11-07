using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Constants;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Hubs;

public sealed class VoiceRoomHub(IVoiceRoomService service) : Hub
{
    internal static string Group(int candidateId, int studentId) => $"voice:{studentId}:{candidateId}";

    public override Task OnConnectedAsync()
    {
        var session = Context.GetHttpContext()?.Session;
        if (session?.GetInt32(SessionKeys.UserId) is not > 0 || session.GetString(SessionKeys.Role) != RoleNames.Student)
        {
            Context.Abort();
            return Task.CompletedTask;
        }
        return base.OnConnectedAsync();
    }

    public async Task Join(int candidateId)
    {
        var session = Context.GetHttpContext()?.Session;
        var studentId = session?.GetInt32(SessionKeys.UserId) ?? 0;
        if (studentId <= 0 || session?.GetString(SessionKeys.Role) != RoleNames.Student)
            throw new HubException("Không có quyền vào phòng thi.");
        var result = await service.CanSubscribeAsync(candidateId, studentId, Context.ConnectionAborted);
        if (!result.Success || !result.Data) throw new HubException("Không có quyền vào lượt thi.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(candidateId, studentId));
    }
}

public sealed class SignalRVoiceRoomNotifier(IHubContext<VoiceRoomHub> hub, ILogger<SignalRVoiceRoomNotifier> logger) : IVoiceRoomNotifier
{
    public async Task ChangedAsync(int candidateId, int studentId, CancellationToken ct)
    {
        try { await hub.Clients.Group(VoiceRoomHub.Group(candidateId, studentId)).SendAsync("VoiceRoomChanged", new { candidateId }, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Voice room notification failed for candidate {CandidateId}", candidateId); }
    }
}
