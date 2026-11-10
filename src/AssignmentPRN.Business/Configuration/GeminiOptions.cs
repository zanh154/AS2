namespace AssignmentPRN.Business;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Chọn bộ AI: "Gemini" (mặc định, không gọi Ollama local), "Auto" (dùng Gemini nếu có ApiKey),
    /// hoặc "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    public string ResolveApiKey() =>
        !string.IsNullOrWhiteSpace(ApiKey)
            ? ApiKey.Trim()
            : (Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim() ?? string.Empty);

    public bool IsGeminiActive
    {
        get
        {
            if (Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)) return false;
            if (Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) return true;
            return !string.IsNullOrWhiteSpace(ResolveApiKey());
        }
    }
}
