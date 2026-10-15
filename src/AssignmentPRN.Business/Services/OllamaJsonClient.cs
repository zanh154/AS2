using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// One call to the local model, asking it for JSON of a given shape.
/// <para>
/// Nothing here throws. An exam is running on the other side of this call, so a model
/// that is switched off, slow or answering with something that is not the JSON we asked
/// for has to look the same to the caller: no answer. The caller then carries on without
/// the AI rather than failing the student's turn.
/// </para>
/// </summary>
public sealed class OllamaJsonClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaOptions> options,
    ILogger<OllamaJsonClient> logger)
{
    private readonly OllamaOptions _options = options.Value;

    /// <summary>
    /// Asks the generation model for one JSON document matching <paramref name="format"/>
    /// (an Ollama structured-output schema). Returns null when there is nothing usable.
    /// </summary>
    /// <param name="timeout">
    /// Wall clock budget. A viva has a question deadline ticking, so the caller passes
    /// what it can actually afford to wait rather than the default five minutes.
    /// </param>
    public async Task<JsonDocument?> GenerateAsync(
        string prompt,
        object format,
        TimeSpan timeout,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);

        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan; // The linked token owns the budget.

            var request = new
            {
                model = _options.GenerationModel,
                prompt,
                stream = false,
                format,
                options = new { temperature = 0.2, top_p = 0.9, num_ctx = 4096, num_predict = maxTokens }
            };

            var response = await client.PostAsJsonAsync("/api/generate", request, budget.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Model returned {Status}; continuing without it.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: budget.Token);
            if (string.IsNullOrWhiteSpace(body?.Response)) return null;

            return JsonDocument.Parse(body.Response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up, not the model. Say nothing and let them unwind.
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Model did not answer within {Timeout}; continuing without it.", timeout);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or UriFormatException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Model call failed; continuing without it.");
            return null;
        }
    }

    private sealed record OllamaGenerateResponse(string Response);
}
