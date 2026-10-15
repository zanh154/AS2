using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssignmentPRN.Business.Services;

/// <summary>
/// Gọi model một lần lúc khởi động để Ollama nạp sẵn nó vào bộ nhớ.
/// <para>
/// Đo trên máy nhóm: lời gọi đầu tiên sau khi bật máy mất 33 giây, trong đó 32 giây là
/// nạp model — vượt hẳn ngân sách 20 giây của <see cref="OllamaFollowUpAnalyzer"/>, nên
/// câu đầu tiên của ca thi đầu tiên mỗi ngày luôn mất câu hỏi xoáy. Khi model đã nằm
/// trong bộ nhớ thì chỉ còn 0,5–3 giây. Trả giá đó ở đây, lúc chưa có ai thi, thay vì để
/// một sinh viên ngồi chờ.
/// </para>
/// <para>
/// Không ai phụ thuộc vào kết quả: Ollama tắt thì chỉ còn một dòng log, ứng dụng vẫn chạy
/// bình thường và phần vấn đáp vẫn hoạt động mà không có AI.
/// </para>
/// </summary>
public sealed class OllamaWarmUpService(
    OllamaJsonClient client,
    ILogger<OllamaWarmUpService> logger) : BackgroundService
{
    /// <summary>
    /// Dài hơn hẳn thời gian nạp đã đo, vì ở đây không có ai phải chờ. Hết hạn thì bỏ.
    /// </summary>
    internal static readonly TimeSpan Budget = TimeSpan.FromMinutes(5);

    /// <summary>Hỏi một câu không cần đúng, chỉ cần model được nạp.</summary>
    private static object Schema => new
    {
        type = "object",
        properties = new { ok = new { type = "boolean" } },
        required = new[] { "ok" }
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Không giữ việc khởi động host lại chờ model nạp.
        await Task.Yield();

        var started = DateTimeOffset.UtcNow;
        using var answer = await client.GenerateAsync(
            "Trả về {\"ok\": true}.", Schema, Budget, maxTokens: 8, stoppingToken);
        var elapsed = DateTimeOffset.UtcNow - started;

        if (stoppingToken.IsCancellationRequested) return;

        if (answer is null)
        {
            logger.LogWarning(
                "Không nạp sẵn được model {Model} sau {Seconds:F1}s. Phần vấn đáp vẫn chạy nhưng không có AI; kiểm tra Ollama đã bật và đã tải model chưa.",
                client.Model, elapsed.TotalSeconds);
            return;
        }

        logger.LogInformation("Đã nạp sẵn model {Model} trong {Seconds:F1}s.", client.Model, elapsed.TotalSeconds);
    }
}
