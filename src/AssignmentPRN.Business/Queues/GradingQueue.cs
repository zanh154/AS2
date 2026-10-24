using System.Runtime.CompilerServices;
using System.Threading.Channels;

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
