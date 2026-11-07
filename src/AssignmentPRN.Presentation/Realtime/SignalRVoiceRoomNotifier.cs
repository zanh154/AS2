using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Presentation.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AssignmentPRN.Presentation.Realtime;

public sealed class SignalRVoiceRoomNotifier(IHubContext<VoiceRoomHub> hub, ILogger<SignalRVoiceRoomNotifier> logger) : IVoiceRoomNotifier
{
    public async Task ChangedAsync(int candidateId, int studentId, CancellationToken ct)
    {
        try { await hub.Clients.Group(VoiceRoomHub.Group(candidateId, studentId)).SendAsync("VoiceRoomChanged", new { candidateId }, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Voice room notification failed for candidate {CandidateId}", candidateId); }
    }
}
