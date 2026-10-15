using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AssignmentPRN.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssignmentPRN.Business;

/// <summary>
/// Slots waiting to be marked. Grading takes a model call per answer, far too long to
/// keep a student's last request open, so handing in only puts the slot in this queue.
/// </summary>
public sealed class GradingQueue
{
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    /// <summary>
    /// Những lượt thi đang chờ. Cùng một lượt có thể được đẩy vào từ nhiều đường — lúc nộp
    /// bài, lúc giảng viên chốt ca, và vòng quét định kỳ — nên hàng đợi tự bỏ bản trùng
    /// thay vì chấm lại hai lần bằng hai lần gọi model.
    /// </summary>
    private readonly HashSet<int> _pending = [];

    public async ValueTask QueueCandidateAsync(int candidateId, CancellationToken cancellationToken = default)
    {
        lock (_pending)
        {
            if (!_pending.Add(candidateId)) return;
        }

        try
        {
            await _queue.Writer.WriteAsync(candidateId, cancellationToken);
        }
        catch
        {
            // Không vào được hàng đợi thì cũng không còn đang chờ, để lần sau đẩy lại được.
            lock (_pending) _pending.Remove(candidateId);
            throw;
        }
    }

    public async IAsyncEnumerable<int> DequeueAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var candidateId in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            // Bỏ khỏi danh sách chờ trước khi chấm: một lượt thi được nộp lại trong lúc
            // đang chấm vẫn phải được chấm lại.
            lock (_pending) _pending.Remove(candidateId);
            yield return candidateId;
        }
    }
}

/// <summary>
/// Marks queued slots one at a time. One at a time on purpose: the local model is
/// configured for a single request, and a second grading in parallel would only make
/// both slower.
/// </summary>
public sealed class GradingBackgroundService(
    GradingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<GradingBackgroundService> logger) : BackgroundService
{
    /// <summary>
    /// Khoảng cách giữa hai vòng quét các lượt thi đã xong mà chưa chấm.
    /// <para>
    /// Lưới an toàn, không phải đường chính: lượt thi được đẩy vào hàng đợi ngay lúc nộp
    /// bài. Nhưng một lượt thi có thể chuyển sang <c>Completed</c> ở chỗ không ai gọi
    /// hàng đợi — hết giờ ca thi, hoặc một đường nộp bài thêm vào sau này — và trước đây
    /// những lượt đó chỉ được chấm khi khởi động lại app. Một phút là đủ nhanh để kết quả
    /// vẫn kịp trong ca thi.
    /// </para>
    /// </summary>
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    /// <summary>Cho test rút ngắn vòng quét; ứng dụng luôn dùng <see cref="SweepInterval"/>.</summary>
    internal TimeSpan Interval { get; init; } = SweepInterval;

    /// <summary>
    /// Những lượt thi đã được chấm (thành công hay không) trong lần chạy này của app.
    /// <para>
    /// "Chưa chấm" trong database nghĩa là chưa có dòng <c>ai_evaluations</c> nào, và một
    /// lượt thi có thể ở mãi trạng thái đó: câu hỏi không có rubric, sinh viên không nói
    /// gì, hoặc model không trả lời. Không nhớ những lượt đã thử thì vòng quét sẽ đưa
    /// chúng vào hàng đợi lại mỗi phút, mãi mãi — tốn một lần gọi model mỗi phút nếu
    /// model hỏng, và chen trước lượt thi thật vừa nộp. Đo thật ngày 15/10/2026 đã thấy
    /// đúng chuyện đó với 7 lượt thi cũ.
    /// </para>
    /// <para>
    /// Chỉ nhớ trong bộ nhớ, nên mỗi lần khởi động lại app vẫn thử lại tất cả một lần —
    /// giữ nguyên cách cũ để chấm bù khi model từng bị tắt.
    /// </para>
    /// </summary>
    private readonly HashSet<int> _attempted = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Quét ngay lúc khởi động, rồi quét lại đều đặn, song song với việc chấm.
        var sweeping = SweepAsync(stoppingToken);
        await ConsumeAsync(stoppingToken);
        await sweeping;
    }

    /// <summary>Gom các lượt thi đã xong mà chưa có đánh giá nào.</summary>
    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var recovery = scopeFactory.CreateScope();
                var repository = recovery.ServiceProvider
                    .GetRequiredService<DataAccess.Repositories.IGradingRepository>();
                foreach (var candidateId in await repository.PendingCandidateIdsAsync(stoppingToken))
                {
                    bool seen;
                    lock (_attempted) seen = _attempted.Contains(candidateId);
                    if (!seen) await queue.QueueCandidateAsync(candidateId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // A database that is not reachable at startup must not stop the host; the
                // slots are still in the table and will be picked up next time.
                logger.LogWarning(ex, "Could not look for slots left ungraded; carrying on.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var candidateId in queue.DequeueAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Ghi nhận trước khi chấm: dù kết quả ra sao, vòng quét không đưa lượt này vào
            // lại nữa. Đường nộp bài vẫn đưa vào trực tiếp được, không qua bộ lọc này.
            lock (_attempted) _attempted.Add(candidateId);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IGradingService>();
                var result = await service.GradeCandidateAsync(candidateId, stoppingToken);
                if (!result.Success)
                {
                    logger.LogWarning("Slot {CandidateId} was not graded: {Error}", candidateId, result.Error);
                }
                else if (result.Data!.SkippedAnswers > 0)
                {
                    logger.LogInformation("Slot {CandidateId}: {Skipped} answers left for the examiner.",
                        candidateId, result.Data.SkippedAnswers);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // One slot that will not grade cannot take the queue down with it.
                logger.LogError(ex, "Grading slot {CandidateId} failed.", candidateId);
            }
        }
    }
}
