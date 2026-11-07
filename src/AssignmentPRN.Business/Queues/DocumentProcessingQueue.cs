using System.Threading.Channels;

namespace AssignmentPRN.Business;

public sealed class DocumentProcessingQueue
{
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask QueueDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(documentId, cancellationToken);
    }

    public async IAsyncEnumerable<int> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var documentId in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            yield return documentId;
        }
    }
}
