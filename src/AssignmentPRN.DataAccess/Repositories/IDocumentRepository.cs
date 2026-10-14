using AssignmentPRN.DataAccess.Entities;

namespace AssignmentPRN.DataAccess.Repositories;

public interface IDocumentRepository
{
    Task<IReadOnlyList<int>> PendingIdsAsync(CancellationToken ct);

    Task<Document> CreateAsync(Document document, CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(int documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListByUserAsync(int userId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int documentId, DocumentStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);

    Task AddChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentAsync(int documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> SearchSimilarChunksAsync(float[] embedding, int courseId, int topK, float threshold, CancellationToken cancellationToken = default, int? documentId = null);

    Task<GeneratedQuestion> CreateQuestionAsync(GeneratedQuestion question, CancellationToken cancellationToken = default);

    Task<GeneratedQuestion?> GetQuestionByIdAsync(int questionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedQuestion>> GetQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedQuestion>> GetQuestionsByCourseAsync(int courseId, GeneratedQuestionStatus? status = null, CancellationToken cancellationToken = default);

    Task UpdateQuestionStatusAsync(int questionId, GeneratedQuestionStatus status, int? reviewedById = null, string? reviewNotes = null, CancellationToken cancellationToken = default);

    Task ReviewAsync(int questionId, Action<GeneratedQuestion> update, CancellationToken ct = default);

    Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken = default);
}