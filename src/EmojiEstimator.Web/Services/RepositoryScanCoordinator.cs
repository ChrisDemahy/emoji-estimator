using System.Collections.Concurrent;
using EmojiEstimator.Web.Data;
using Microsoft.Extensions.DependencyInjection;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanCoordinator(
    IRepositoryScanBackgroundQueue backgroundQueue,
    IRepositoryScanProgressNotifier progressNotifier,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider) : IRepositoryScanCoordinator
{
    private readonly ConcurrentDictionary<string, byte> activeScans = new(StringComparer.Ordinal);

    public async Task<RepositoryScanProgressUpdate?> GetCurrentStateAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var normalizedKey = RepositoryScan.CreateNormalizedKey(trimmedOwner, trimmedRepository);

        var activeSnapshot = TryGetActiveSnapshot(normalizedKey, trimmedOwner, trimmedRepository);
        if (activeSnapshot is not null)
        {
            return activeSnapshot;
        }

        var currentScan = await GetCurrentScanAsync(trimmedOwner, trimmedRepository, cancellationToken);
        if (currentScan is null)
        {
            progressNotifier.Clear(normalizedKey);
            return null;
        }

        var currentSnapshot = RepositoryScanProgressUpdate.FromPersistedScan(currentScan);
        progressNotifier.Store(currentSnapshot);
        return currentSnapshot;
    }

    public async Task<RepositoryScanProgressUpdate> QueueScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var normalizedKey = RepositoryScan.CreateNormalizedKey(trimmedOwner, trimmedRepository);

        var activeSnapshot = TryGetActiveSnapshot(normalizedKey, trimmedOwner, trimmedRepository);
        if (activeSnapshot is not null)
        {
            return activeSnapshot;
        }

        var currentScan = await GetCurrentScanAsync(trimmedOwner, trimmedRepository, cancellationToken);
        if (currentScan is not null &&
            string.Equals(currentScan.Status, RepositoryScanStatuses.Completed, StringComparison.Ordinal))
        {
            var completedSnapshot = RepositoryScanProgressUpdate.FromPersistedScan(currentScan);
            progressNotifier.Store(completedSnapshot);
            return completedSnapshot;
        }

        if (!activeScans.TryAdd(normalizedKey, 0))
        {
            return progressNotifier.GetLatest(normalizedKey)
                ?? RepositoryScanProgressUpdate.CreatePending(trimmedOwner, trimmedRepository, timeProvider.GetUtcNow());
        }

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var scanStore = scope.ServiceProvider.GetRequiredService<IRepositoryScanStore>();
            var pendingScan = await scanStore.SavePendingScanAsync(trimmedOwner, trimmedRepository, cancellationToken);
            var pendingSnapshot = RepositoryScanProgressUpdate.FromPersistedScan(pendingScan);

            await progressNotifier.PublishAsync(pendingSnapshot, cancellationToken);
            await backgroundQueue.QueueAsync(
                new RepositoryScanWorkItem(trimmedOwner, trimmedRepository, normalizedKey),
                cancellationToken);

            return pendingSnapshot;
        }
        catch
        {
            activeScans.TryRemove(normalizedKey, out _);
            progressNotifier.Clear(normalizedKey);
            throw;
        }
    }

    internal void CompleteQueuedScan(string normalizedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);
        activeScans.TryRemove(normalizedKey, out _);
    }

    private RepositoryScanProgressUpdate? TryGetActiveSnapshot(
        string normalizedKey,
        string owner,
        string repository)
    {
        if (!activeScans.ContainsKey(normalizedKey))
        {
            return null;
        }

        return progressNotifier.GetLatest(normalizedKey)
            ?? RepositoryScanProgressUpdate.CreatePending(owner, repository, timeProvider.GetUtcNow());
    }

    private async Task<RepositoryScan?> GetCurrentScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var scanStore = scope.ServiceProvider.GetRequiredService<IRepositoryScanStore>();
        return await scanStore.GetCurrentScanOrDeleteStaleAsync(owner, repository, cancellationToken);
    }
}
