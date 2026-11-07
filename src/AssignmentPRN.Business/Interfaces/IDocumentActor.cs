namespace AssignmentPRN.Business.Interfaces;
public interface IDocumentActor { int UserId { get; } string? Role { get; } }
public sealed record DocumentProgress(int DocumentId, int CourseId, string Status);
public interface IDocumentProgressNotifier { Task ChangedAsync(DocumentProgress progress, CancellationToken ct = default); }
