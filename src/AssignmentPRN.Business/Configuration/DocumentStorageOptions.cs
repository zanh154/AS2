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
