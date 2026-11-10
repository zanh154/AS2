using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Hợp đồng chung cho mọi bộ AI sinh JSON theo schema (Ollama local hoặc Google Gemini).
/// Mọi cài đặt không được ném ngoại lệ ra ngoài để phòng thi luôn vận hành an toàn.
/// </summary>
public interface IAiJsonClient
{
    string Model { get; }

    Task<JsonDocument?> GenerateAsync(
        string prompt,
        object format,
        TimeSpan timeout,
        int maxTokens,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gọi Google Gemini API (<c>generateContent</c>) trả về JSON có cấu trúc theo schema.
/// </summary>
public sealed class GeminiJsonClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> options,
    ILogger<GeminiJsonClient> logger) : IAiJsonClient
{
    private readonly GeminiOptions _options = options.Value;

    public string Model => string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.5-flash" : _options.Model.Trim();

    public async Task<JsonDocument?> GenerateAsync(
        string prompt,
        object format,
        TimeSpan timeout,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Gemini API key is missing; cannot call Gemini model {Model}.", Model);
            return null;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? "https://generativelanguage.googleapis.com"
                : _options.BaseUrl.TrimEnd('/');
            var endpoint = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent";

            var payload = BuildRequestPayload(prompt, format, maxTokens, Model);
            var client = httpClientFactory.CreateClient();
            client.Timeout = Timeout.InfiniteTimeSpan;

            HttpResponseMessage? response = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("x-goog-api-key", apiKey);
                req.Content = JsonContent.Create(payload);

                response?.Dispose();
                response = await client.SendAsync(req, budget.Token);
                if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable) || attempt == 1)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1.5), budget.Token);
            }

            if (response is null || !response.IsSuccessStatusCode)
            {
                var status = response is null ? 0 : (int)response.StatusCode;
                logger.LogWarning("Gemini returned HTTP {Status}; continuing without AI.", status);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(budget.Token),
                cancellationToken: budget.Token);

            var text = ExtractCandidateText(doc);
            if (string.IsNullOrWhiteSpace(text)) return null;

            return JsonDocument.Parse(StripCodeFences(text));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Gemini did not answer within {Timeout}; continuing without it.", timeout);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or UriFormatException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Gemini call failed; continuing without it.");
            return null;
        }
    }

    internal static JsonObject BuildRequestPayload(string prompt, object format, int maxTokens, string model)
    {
        var schemaNode = SanitizeSchemaForGemini(JsonSerializer.SerializeToNode(format));
        var isGemini25 = model.Contains("2.5", StringComparison.OrdinalIgnoreCase);

        var genConfig = new JsonObject
        {
            ["temperature"] = 0.2,
            ["topP"] = 0.9,
            ["maxOutputTokens"] = Math.Max(maxTokens, 2048),
            ["responseMimeType"] = "application/json",
            ["responseSchema"] = schemaNode
        };
        if (isGemini25)
        {
            genConfig["thinkingConfig"] = new JsonObject { ["thinkingBudget"] = 0 };
        }

        return new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = prompt }
                    }
                }
            },
            ["generationConfig"] = genConfig
        };
    }

    /// <summary>
    /// Gemini OpenAPI responseSchema chỉ chấp nhận enum dạng chuỗi; bỏ enum trên trường số nguyên
    /// để tránh HTTP 400 (prompt đã ràng buộc giá trị id hợp lệ).
    /// </summary>
    internal static JsonNode? SanitizeSchemaForGemini(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var typeName = obj["type"]?.GetValue<string>();
            if (obj.ContainsKey("enum") && !string.Equals(typeName, "string", StringComparison.OrdinalIgnoreCase))
            {
                obj.Remove("enum");
            }

            foreach (var kvp in obj.ToList())
            {
                SanitizeSchemaForGemini(kvp.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                SanitizeSchemaForGemini(item);
            }
        }

        return node;
    }

    private static string? ExtractCandidateText(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
                continue;
            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    var text = textEl.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }

        return null;
    }

    private static string StripCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline >= 0 && lastFence > firstNewline)
        {
            return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }
}

/// <summary>
/// Tự động điều hướng lời gọi AI sang <see cref="GeminiJsonClient"/> khi cấu hình dùng Gemini
/// (hoặc có API key), và dùng <see cref="OllamaJsonClient"/> khi chọn Ollama / chưa gắn key.
/// </summary>
public sealed class RoutingAiJsonClient(
    IOptions<GeminiOptions> geminiOptions,
    GeminiJsonClient geminiClient,
    OllamaJsonClient ollamaClient) : IAiJsonClient
{
    private IAiJsonClient Active => geminiOptions.Value.IsGeminiActive ? geminiClient : ollamaClient;

    public string Model => Active.Model;

    public Task<JsonDocument?> GenerateAsync(
        string prompt,
        object format,
        TimeSpan timeout,
        int maxTokens,
        CancellationToken cancellationToken = default) =>
        Active.GenerateAsync(prompt, format, timeout, maxTokens, cancellationToken);
}
