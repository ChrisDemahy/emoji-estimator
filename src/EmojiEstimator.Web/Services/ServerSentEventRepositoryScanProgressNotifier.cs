using System.Collections.Concurrent;
using System.Threading.Channels;

namespace EmojiEstimator.Web.Services;

public sealed class ServerSentEventRepositoryScanProgressNotifier : IRepositoryScanProgressNotifier
{
    private readonly ConcurrentDictionary<string, RepositoryScanProgressUpdate> latestUpdates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SubscriptionCollection> subscriptions = new(StringComparer.Ordinal);

    public RepositoryScanProgressUpdate? GetLatest(string normalizedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);

        return latestUpdates.TryGetValue(normalizedKey, out RepositoryScanProgressUpdate? update)
            ? update
            : null;
    }

    public RepositoryScanProgressSubscription Subscribe(string normalizedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);

        SubscriptionCollection subscriptionCollection = subscriptions.GetOrAdd(
            normalizedKey,
            _ => new SubscriptionCollection());
        Channel<RepositoryScanProgressUpdate> channel = Channel.CreateUnbounded<RepositoryScanProgressUpdate>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        lock (subscriptionCollection.SyncRoot)
        {
            subscriptionCollection.Channels.Add(channel);
        }

        return new RepositoryScanProgressSubscription(
            channel.Reader,
            () => Unsubscribe(normalizedKey, subscriptionCollection, channel));
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

    public Task PublishAsync(
        RepositoryScanProgressUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        Store(update);

        if (!subscriptions.TryGetValue(update.NormalizedKey, out SubscriptionCollection? subscriptionCollection))
        {
            return Task.CompletedTask;
        }

        List<Channel<RepositoryScanProgressUpdate>> channels;
        lock (subscriptionCollection.SyncRoot)
        {
            channels = [.. subscriptionCollection.Channels];
        }

        foreach (Channel<RepositoryScanProgressUpdate> channel in channels)
        {
            channel.Writer.TryWrite(update);
        }

        return Task.CompletedTask;
    }

    private void Unsubscribe(
        string normalizedKey,
        SubscriptionCollection subscriptionCollection,
        Channel<RepositoryScanProgressUpdate> channel)
    {
        lock (subscriptionCollection.SyncRoot)
        {
            subscriptionCollection.Channels.Remove(channel);

            if (subscriptionCollection.Channels.Count == 0 &&
                subscriptions.TryGetValue(normalizedKey, out SubscriptionCollection? existingCollection) &&
                ReferenceEquals(existingCollection, subscriptionCollection))
            {
                subscriptions.TryRemove(normalizedKey, out _);
            }
        }

        channel.Writer.TryComplete();
    }

    private sealed class SubscriptionCollection
    {
        public object SyncRoot { get; } = new();

        public List<Channel<RepositoryScanProgressUpdate>> Channels { get; } = [];
    }
}
