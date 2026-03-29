using EmojiEstimator.Web.Data;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanner(
    IRepositoryScanStore scanStore,
    IGitHubContentReader contentReader,
    IRepositoryScanAggregator scanAggregator,
    IRepositoryScanProgressNotifier progressNotifier,
    TimeProvider timeProvider) : IRepositoryScanner
{
    private const int LockStripeCount = 64;
    private static readonly SemaphoreSlim[] ScanLocks = Enumerable.Range(0, LockStripeCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async Task<RepositoryScanResult> ScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var normalizedKey = RepositoryScan.CreateNormalizedKey(trimmedOwner, trimmedRepository);
        var scanLock = GetScanLock(normalizedKey);

        await scanLock.WaitAsync(cancellationToken);

        try
        {
            var existingScan = await scanStore.GetFreshScanOrDeleteStaleAsync(
                trimmedOwner,
                trimmedRepository,
                cancellationToken);

            if (existingScan is not null)
            {
                return DeserializeStoredResult(existingScan);
            }

            var runningScan = await scanStore.SaveRunningScanAsync(trimmedOwner, trimmedRepository, cancellationToken);
            await progressNotifier.PublishAsync(
                RepositoryScanProgressUpdate.FromPersistedScan(runningScan),
                cancellationToken);

            var contentItems = await contentReader.ReadAllAsync(
                trimmedOwner,
                trimmedRepository,
                async (progress, progressCancellationToken) =>
                {
                    await progressNotifier.PublishAsync(
                        RepositoryScanProgressUpdate.CreateRunningPageProgress(
                            trimmedOwner,
                            trimmedRepository,
                            progress,
                            GetUtcNow()),
                        progressCancellationToken);
                },
                cancellationToken);
            var result = scanAggregator.Aggregate(trimmedOwner, trimmedRepository, contentItems);
            var resultJson = RepositoryScanResultSerializer.Serialize(result);

            var completedScan = await scanStore.SaveCompletedScanAsync(
                trimmedOwner,
                trimmedRepository,
                resultJson,
                cancellationToken);
            await progressNotifier.PublishAsync(
                RepositoryScanProgressUpdate.FromPersistedScan(completedScan),
                cancellationToken);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedScan = await scanStore.SaveFailedScanAsync(
                trimmedOwner,
                trimmedRepository,
                CreateFailureMessage(exception),
                cancellationToken);
            await progressNotifier.PublishAsync(
                RepositoryScanProgressUpdate.FromPersistedScan(failedScan),
                cancellationToken);

            throw;
        }
        finally
        {
            scanLock.Release();
        }
    }

    private static RepositoryScanResult DeserializeStoredResult(RepositoryScan scan)
    {
        if (string.IsNullOrWhiteSpace(scan.ResultJson))
        {
            throw new InvalidOperationException("The stored repository scan is missing serialized results.");
        }

        return RepositoryScanResultSerializer.Deserialize(scan.ResultJson);
    }

    private static SemaphoreSlim GetScanLock(string normalizedKey)
    {
        var stripeIndex = (StringComparer.Ordinal.GetHashCode(normalizedKey) & int.MaxValue) % LockStripeCount;
        return ScanLocks[stripeIndex];
    }

    private static string CreateFailureMessage(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return string.IsNullOrWhiteSpace(baseException.Message)
            ? "The repository scan failed."
            : baseException.Message.Trim();
    }

    private DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
}
