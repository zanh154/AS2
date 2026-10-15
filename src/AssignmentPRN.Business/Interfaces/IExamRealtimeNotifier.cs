namespace AssignmentPRN.Business.Interfaces;
/// <summary>Invalidation signal after persistence; contains no answers or personal details.</summary>
public sealed record ExamChange(int ExamId, int LecturerId, IReadOnlyList<int> StudentIds, string Kind);
public interface IExamRealtimeNotifier
{
    Task ChangedAsync(ExamChange change, CancellationToken ct = default);
}
