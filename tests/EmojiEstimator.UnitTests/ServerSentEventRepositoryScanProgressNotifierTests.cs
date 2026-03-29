using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class ServerSentEventRepositoryScanProgressNotifierTests
{
    [Fact]
    public async Task PublishAsyncStoresTheLatestUpdateAndStreamsItToSubscribers()
    {
        var notifier = new ServerSentEventRepositoryScanProgressNotifier();
        using RepositoryScanProgressSubscription subscription = notifier.Subscribe("DOTNET/ASPNETCORE");
        var update = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = "Running",
            Message = "Fetched pull request page 1.",
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
        };

        await notifier.PublishAsync(update);

        RepositoryScanProgressUpdate streamedUpdate = await subscription.Reader.ReadAsync(CancellationToken.None);

        Assert.Same(update, streamedUpdate);
        Assert.Same(update, notifier.GetLatest("DOTNET/ASPNETCORE"));
    }

    [Fact]
    public async Task DisposedSubscriptionStopsReceivingUpdates()
    {
        var notifier = new ServerSentEventRepositoryScanProgressNotifier();
        var update = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = "Completed",
            Message = "Scan completed.",
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
        };
        RepositoryScanProgressSubscription subscription = notifier.Subscribe("DOTNET/ASPNETCORE");

        subscription.Dispose();
        await notifier.PublishAsync(update);

        Assert.False(await subscription.Reader.WaitToReadAsync(CancellationToken.None));
    }

    [Fact]
    public void Store_SavesUpdateAndMakesItRetrievableByKey()
    {
        var notifier = new ServerSentEventRepositoryScanProgressNotifier();
        var update = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = "Running",
            Message = "Fetched pull request page 1.",
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
        };

        notifier.Store(update);

        Assert.Same(update, notifier.GetLatest("DOTNET/ASPNETCORE"));
        Assert.Null(notifier.GetLatest("DOTNET/RUNTIME"));
    }

    [Fact]
    public async Task Clear_RemovesStoredUpdate()
    {
        var notifier = new ServerSentEventRepositoryScanProgressNotifier();
        var update = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = "Completed",
            Message = "Scan completed.",
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
        };
        await notifier.PublishAsync(update);
        Assert.NotNull(notifier.GetLatest("DOTNET/ASPNETCORE"));

        notifier.Clear("DOTNET/ASPNETCORE");

        Assert.Null(notifier.GetLatest("DOTNET/ASPNETCORE"));
    }

    [Fact]
    public async Task PublishAsyncStreamsUpdatesToEverySubscriberForTheMatchingRepository()
    {
        var notifier = new ServerSentEventRepositoryScanProgressNotifier();
        using RepositoryScanProgressSubscription firstMatchingSubscription = notifier.Subscribe("DOTNET/ASPNETCORE");
        using RepositoryScanProgressSubscription secondMatchingSubscription = notifier.Subscribe("DOTNET/ASPNETCORE");
        using RepositoryScanProgressSubscription otherSubscription = notifier.Subscribe("DOTNET/RUNTIME");
        var update = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = "Running",
            Message = "Fetched issue page 2.",
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
        };

        await notifier.PublishAsync(update);

        Assert.Same(update, await firstMatchingSubscription.Reader.ReadAsync(CancellationToken.None));
        Assert.Same(update, await secondMatchingSubscription.Reader.ReadAsync(CancellationToken.None));
        Assert.False(otherSubscription.Reader.TryRead(out _));
    }
}
