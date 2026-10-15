using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.SignalR;
namespace AssignmentPRN.Presentation.Hubs;
public sealed class SignalRExamNotifier(IHubContext<ExamUpdatesHub> hub, ILogger<SignalRExamNotifier> logger) : IExamRealtimeNotifier
{
    public async Task ChangedAsync(ExamChange change, CancellationToken ct = default)
    {
        var groups = change.StudentIds.Append(change.LecturerId).Distinct().Select(ExamGroups.User)
            .Append(ExamGroups.Admins).ToArray();
        try { await hub.Clients.Groups(groups).SendAsync("ExamChanged", new { examId = change.ExamId, kind = change.Kind }, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Realtime notification failed after saving exam {ExamId}", change.ExamId); }
    }
}
