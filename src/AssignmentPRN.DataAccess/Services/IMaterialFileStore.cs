namespace AssignmentPRN.DataAccess.Services;

/// <summary>
/// Persists the bytes of a course material independently from its database metadata.
/// Callers only keep the returned storage-relative path.
/// </summary>
public interface IMaterialFileStore
{
    Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storedPath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string? storedPath,
        CancellationToken cancellationToken = default);
}
