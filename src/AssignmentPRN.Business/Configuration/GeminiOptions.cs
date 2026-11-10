namespace AssignmentPRN.Business;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Chọn bộ AI suy luận: "Auto" (tự dùng Gemini nếu có ApiKey, ngược lại dùng Ollama),
    /// "Gemini" hoặc "Ollama".
    /// </summary>
    public string Provider { get; set; } = "Auto";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.5-flash";

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
            var hasKey = !string.IsNullOrWhiteSpace(ResolveApiKey());
            if (Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) return hasKey;
            return hasKey; // Auto
        }
    }
}
