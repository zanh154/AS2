using AssignmentPRN.Business.Interfaces;
using Microsoft.AspNetCore.SignalR;
using AssignmentPRN.Presentation.Hubs;
namespace AssignmentPRN.Presentation.Realtime;
public sealed class SignalRExamNotifier(IHubContext<ExamUpdatesHub> hub, ILogger<SignalRExamNotifier> logger) : IExamRealtimeNotifier
{
    public async Task ChangedAsync(ExamChange change, CancellationToken ct = default)
    {
        var groups = change.StudentIds.Append(change.LecturerId).Where(id => id > 0).Distinct().Select(ExamGroups.User)
            .Append(ExamGroups.Admins);
        if (change.CourseId is > 0)
        {
            groups = groups.Append(ExamGroups.Course(change.CourseId.Value));
        }

        try
        {
            await hub.Clients.Groups(groups.Distinct().ToArray()).SendAsync(
                "ExamChanged",
                new { examId = change.ExamId, courseId = change.CourseId, kind = change.Kind },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime notification failed for {Kind}, exam {ExamId}, course {CourseId}",
                change.Kind, change.ExamId, change.CourseId);
        }
    }
}
