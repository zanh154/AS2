using AssignmentPRN.Business;

namespace AssignmentPRN.Tests;

/// <summary>
/// Hàng đợi chấm điểm. Cùng một lượt thi được đẩy vào từ nhiều đường — nộp bài, giảng viên
/// chốt ca, vòng quét định kỳ — nên điều đáng kiểm tra là nó không chấm trùng, và không
/// đánh rơi lượt nào.
/// </summary>
public sealed class GradingQueueTests
{
    /// <summary>Đọc hết những gì đang có trong hàng đợi rồi dừng.</summary>
    internal static async Task<List<int>> DrainAsync(GradingQueue queue)
    {
        var ids = new List<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try
        {
            await foreach (var id in queue.DequeueAsync(cts.Token)) ids.Add(id);
        }
        catch (OperationCanceledException) { /* Hết hàng đợi. */ }
        return ids;
    }

    [Fact]
    public async Task A_slot_queued_twice_is_only_graded_once()
    {
        var queue = new GradingQueue();

        await queue.QueueCandidateAsync(7);
        await queue.QueueCandidateAsync(7);
        await queue.QueueCandidateAsync(8);

        Assert.Equal([7, 8], await DrainAsync(queue));
    }

    [Fact]
    public async Task A_slot_queued_again_after_it_was_taken_out_is_graded_again()
    {
        // Nộp lại hoặc chấm lại thì lượt thi phải được chấm lần nữa; chống trùng chỉ áp
        // dụng cho những lượt đang còn chờ trong hàng đợi.
        var queue = new GradingQueue();
        await queue.QueueCandidateAsync(7);
        Assert.Equal([7], await DrainAsync(queue));

        await queue.QueueCandidateAsync(7);

        Assert.Equal([7], await DrainAsync(queue));
    }
}
