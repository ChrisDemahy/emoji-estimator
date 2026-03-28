using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanBackgroundService(
    IRepositoryScanBackgroundQueue backgroundQueue,
    RepositoryScanCoordinator scanCoordinator,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RepositoryScanBackgroundService> logger) : BackgroundService
{
    private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount, 1, 4);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, WorkerCount)
            .Select(_ => RunWorkerAsync(stoppingToken));

        return Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            RepositoryScanWorkItem workItem;

            try
            {
                workItem = await backgroundQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessWorkItemAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Repository scan processing failed for {RepositoryOwner}/{RepositoryName}.",
                    workItem.RepositoryOwner,
                    workItem.RepositoryName);
            }
        }
    }

    private async Task ProcessWorkItemAsync(
        RepositoryScanWorkItem workItem,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repositoryScanner = scope.ServiceProvider.GetRequiredService<IRepositoryScanner>();

        try
        {
            await repositoryScanner.ScanAsync(
                workItem.RepositoryOwner,
                workItem.RepositoryName,
                cancellationToken);
        }
        finally
        {
            scanCoordinator.CompleteQueuedScan(workItem.NormalizedKey);
        }
    }
}
