using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.SignalR;
using AssignmentPRN.Presentation.Hubs;
namespace AssignmentPRN.Presentation.Realtime;
public sealed class SignalRDocumentNotifier(IHubContext<DocumentProcessingHub> hub, ILogger<SignalRDocumentNotifier> logger) : IDocumentProgressNotifier
{
    public async Task ChangedAsync(DocumentProgress progress, CancellationToken ct = default) {
        try { await hub.Clients.Groups([DocumentProcessingHub.DocumentGroup(progress.DocumentId), DocumentProcessingHub.CourseGroup(progress.CourseId)]).SendAsync("DocumentProgress", new { documentId = progress.DocumentId, status = progress.Status }, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to notify document {DocumentId}", progress.DocumentId); }
    }
}
