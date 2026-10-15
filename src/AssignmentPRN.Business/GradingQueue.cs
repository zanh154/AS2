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

    public ValueTask QueueCandidateAsync(int candidateId, CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(candidateId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAsync(CancellationToken cancellationToken = default) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A slot that finished while the app was down is still waiting to be marked.
        try
        {
            using var recovery = scopeFactory.CreateScope();
            var repository = recovery.ServiceProvider
                .GetRequiredService<DataAccess.Repositories.IGradingRepository>();
            foreach (var candidateId in await repository.PendingCandidateIdsAsync(stoppingToken))
            {
                await queue.QueueCandidateAsync(candidateId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            // A database that is not reachable at startup must not stop the host; the
            // slots are still in the table and will be picked up next time.
            logger.LogWarning(ex, "Could not look for slots left ungraded; carrying on.");
        }

        await foreach (var candidateId in queue.DequeueAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;

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
