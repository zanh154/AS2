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

    /// <summary>
    /// Model dùng cho hỏi xoáy và chấm điểm vấn đáp. Tách khỏi
    /// <see cref="GenerationModel"/> vì hai việc này cần đọc hiểu tiếng Việt và suy luận
    /// theo rubric: đo trên máy nhóm, qwen2.5:3b sinh ra 0/10 câu hỏi thêm dùng được, còn
    /// bản 7b thì đạt. Sinh câu hỏi từ tài liệu (RAG) vẫn dùng model nhỏ cho nhanh.
    /// Để trống thì quay về <see cref="GenerationModel"/>, nên máy chưa tải model 7b vẫn
    /// chạy được, chỉ là chất lượng kém hơn.
    /// </summary>
    public string VivaModel { get; set; } = "qwen2.5:7b-instruct";
}

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public int TopK { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.7f;
}