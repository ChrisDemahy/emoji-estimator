using System.Collections.Concurrent;
using EmojiEstimator.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EmojiEstimator.Web.Services;

public sealed class SignalRRepositoryScanProgressNotifier(
    IHubContext<RepositoryScanHub, IRepositoryScanClient> hubContext) : IRepositoryScanProgressNotifier
{
    private readonly ConcurrentDictionary<string, RepositoryScanProgressUpdate> latestUpdates = new(StringComparer.Ordinal);

    public RepositoryScanProgressUpdate? GetLatest(string normalizedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);

        return latestUpdates.TryGetValue(normalizedKey, out var update)
            ? update
            : null;
    }

    public void Store(RepositoryScanProgressUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        latestUpdates[update.NormalizedKey] = update;
    }

    public void Clear(string normalizedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);
        latestUpdates.TryRemove(normalizedKey, out _);
    }

    public async Task PublishAsync(
        RepositoryScanProgressUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        Store(update);
        await hubContext.Clients.Group(
                RepositoryScanProgressUpdate.CreateGroupName(
                    update.RepositoryOwner,
                    update.RepositoryName))
            .ScanUpdated(update);
    }
}
