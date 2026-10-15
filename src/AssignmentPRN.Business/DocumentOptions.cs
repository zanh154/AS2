namespace AssignmentPRN.Business;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string BasePath { get; set; } = "../AssignmentPRN.DataAccess/Storage/rag";
    public int MaxFileSizeMB { get; set; } = 50;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".pptx"];
    public int ChunkSize { get; set; } = 800;
    public int ChunkOverlap { get; set; } = 100;
}

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string GenerationModel { get; set; } = "qwen2.5:3b";
}

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public int TopK { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.7f;
}