using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssignmentPRN.Tests;

/// <summary>
/// Vòng quét định kỳ của <see cref="GradingBackgroundService"/>. Lỗi đo được ngày
/// 15/10/2026: các lượt thi không chấm được (không có rubric, model không trả lời) vẫn
/// "chưa chấm" trong database, nên bị đưa vào hàng đợi lại mỗi phút, mãi mãi.
/// </summary>
public sealed class GradingSweepTests
{
    /// <summary>Luôn báo cùng một danh sách lượt thi "chưa chấm", như database thật.</summary>
    private sealed class StuckRepository(params int[] pending) : IGradingRepository
    {
        public Task<IReadOnlyList<int>> PendingCandidateIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(pending);

        public Task<ReviewRow?> LoadReviewAsync(int candidateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AdjustAsync(int candidateId, int examId, int lecturerId, decimal? score, decimal? finalScore,
            string? comment, string auditLine, DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ResultAuditRow>> AuditTrailAsync(int candidateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(int StudentId, bool Completed)?> SlotOwnerAsync(int candidateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GradingSlotRow?> LoadAsync(int candidateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(int candidateId, int examId, IReadOnlyList<AnswerEvaluation> evaluations, decimal? slotScore,
            DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Đếm số lần mỗi lượt thi được chấm; không bao giờ lưu gì, như khi bị bỏ qua.</summary>
    private sealed class CountingGrader : IGradingService
    {
        private readonly List<int> _calls = [];

        public IReadOnlyList<int> Calls { get { lock (_calls) return [.. _calls]; } }

        public Task<ServiceResponse<GradingOutcome>> GradeCandidateAsync(int candidateId, CancellationToken ct = default)
        {
            lock (_calls) _calls.Add(candidateId);
            return Task.FromResult(ServiceResponse<GradingOutcome>.Ok(new GradingOutcome(0, 1, 0, null)));
        }
    }

    private static async Task<(CountingGrader Grader, GradingQueue Queue)> RunAsync(
        IGradingRepository repository, TimeSpan runFor, Func<GradingQueue, Task>? during = null)
    {
        var grader = new CountingGrader();
        var queue = new GradingQueue();
        var services = new ServiceCollection()
            .AddSingleton(repository)
            .AddSingleton<IGradingService>(grader)
            .BuildServiceProvider();

        var sweeper = new GradingBackgroundService(queue, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GradingBackgroundService>.Instance) { Interval = TimeSpan.FromMilliseconds(40) };

        await sweeper.StartAsync(CancellationToken.None);
        if (during is not null) await during(queue);
        await Task.Delay(runFor);
        await sweeper.StopAsync(CancellationToken.None);
        return (grader, queue);
    }

    [Fact]
    public async Task A_slot_that_never_gets_a_mark_is_not_swept_again_and_again()
    {
        // Khoảng 10 vòng quét trong lúc chạy; mỗi lượt vẫn chỉ được thử một lần.
        var (grader, _) = await RunAsync(new StuckRepository(10, 23), TimeSpan.FromMilliseconds(450));

        Assert.Equal([10, 23], grader.Calls.Order());
    }

    [Fact]
    public async Task A_slot_handed_in_while_running_is_still_graded_straight_away()
    {
        // Bộ lọc chỉ áp dụng cho vòng quét; đường nộp bài vẫn đưa thẳng vào hàng đợi.
        var (grader, _) = await RunAsync(new StuckRepository(), TimeSpan.FromMilliseconds(200),
            queue => queue.QueueCandidateAsync(75).AsTask());

        Assert.Equal([75], grader.Calls);
    }
}
