namespace AssignmentPRN.DataAccess.Entities;

public class DocumentChunk
{
    public int ChunkId { get; set; }

    public int DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }

    // Vector embedding stored as JSON array in MySQL (fallback for older versions)
    // For MySQL 8.4+, use VECTOR type via raw SQL migration
    public string EmbeddingJson { get; set; } = "[]";

    public Document Document { get; set; } = null!;
}