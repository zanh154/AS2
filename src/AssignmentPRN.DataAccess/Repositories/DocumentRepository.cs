using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AssignmentPRN.DataAccess.Repositories;

public sealed class DocumentRepository(AivesDbContext context) : IDocumentRepository
{
    public async Task<Document> CreateAsync(Document document, CancellationToken cancellationToken = default)
    {
        context.Documents.Add(document);
        await context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<Document?> GetByIdAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await context.Documents
            .Include(d => d.Course)
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> ListByCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        return await context.Documents
            .Where(d => d.CourseId == courseId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> ListByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await context.Documents
            .Where(d => d.UploadedById == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(int documentId, DocumentStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var doc = await context.Documents.FindAsync([documentId], cancellationToken);
        if (doc is not null)
        {
            doc.Status = status;
            doc.ErrorMessage = errorMessage;
            if (status == DocumentStatus.Completed || status == DocumentStatus.Failed)
            {
                doc.ProcessedAt = DateTime.UtcNow;
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        context.DocumentChunks.AddRange(chunks);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchSimilarChunksAsync(float[] embedding, int courseId, int topK, float threshold, CancellationToken cancellationToken = default)
    {
        // For MySQL without native VECTOR support, we do cosine similarity in memory
        // For production with MySQL 8.4+, use native VECTOR index and SQL
        var query = context.DocumentChunks
            .Where(c => c.Document!.CourseId == courseId)
            .Select(c => new { c.ChunkId, c.DocumentId, c.ChunkIndex, c.Content, c.TokenCount, c.EmbeddingJson });

        var allChunks = await query.ToListAsync(cancellationToken);

        var embeddingVector = embedding;
        var results = allChunks
            .Select(c => new
            {
                Chunk = c,
                Similarity = CosineSimilarity(embeddingVector, JsonSerializer.Deserialize<float[]>(c.EmbeddingJson) ?? [])
            })
            .Where(x => x.Similarity >= threshold)
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .Select(x => new DocumentChunk
            {
                ChunkId = x.Chunk.ChunkId,
                DocumentId = x.Chunk.DocumentId,
                ChunkIndex = x.Chunk.ChunkIndex,
                Content = x.Chunk.Content,
                TokenCount = x.Chunk.TokenCount,
                EmbeddingJson = x.Chunk.EmbeddingJson
            })
            .ToList();

        return results;
    }

    public async Task<GeneratedQuestion> CreateQuestionAsync(GeneratedQuestion question, CancellationToken cancellationToken = default)
    {
        context.GeneratedQuestions.Add(question);
        await context.SaveChangesAsync(cancellationToken);
        return question;
    }

    public async Task<GeneratedQuestion?> GetQuestionByIdAsync(int questionId, CancellationToken cancellationToken = default)
    {
        return await context.GeneratedQuestions
            .Include(q => q.Document)
            .Include(q => q.Course)
            .Include(q => q.ReviewedBy)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId, cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedQuestion>> GetQuestionsByDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await context.GeneratedQuestions
            .Where(q => q.DocumentId == documentId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedQuestion>> GetQuestionsByCourseAsync(int courseId, GeneratedQuestionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = context.GeneratedQuestions
            .Where(q => q.CourseId == courseId);

        if (status.HasValue)
        {
            query = query.Where(q => q.Status == status.Value);
        }

        return await query
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateQuestionStatusAsync(int questionId, GeneratedQuestionStatus status, int? reviewedById = null, string? reviewNotes = null, CancellationToken cancellationToken = default)
    {
        var question = await context.GeneratedQuestions.FindAsync([questionId], cancellationToken);
        if (question is not null)
        {
            question.Status = status;
            question.ReviewedById = reviewedById;
            question.ReviewedAt = DateTime.UtcNow;
            question.ReviewNotes = reviewNotes;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var doc = await context.Documents.FindAsync([documentId], cancellationToken);
        if (doc is not null)
        {
            context.Documents.Remove(doc);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f) return 0f;
        return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}