namespace AssignmentPRN.Business;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public int TopK { get; set; } = 5;
    public float SimilarityThreshold { get; set; } = 0.7f;
}
