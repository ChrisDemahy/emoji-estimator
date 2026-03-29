using System.Text.Json;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanCoordinatorTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task QueueScanAsync_DeduplicatesConcurrentQueueRequests()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeRepositoryScanStore();
        store.BlockPendingSave();
        var queue = new FakeRepositoryScanBackgroundQueue();
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var coordinator = new RepositoryScanCoordinator(
            queue,
            notifier,
            new FakeServiceScopeFactory(store),
            new FixedTimeProvider(utcNow));

        var firstQueueTask = coordinator.QueueScanAsync("octocat", "hello-world");
        await store.PendingSaveStarted.Task;

        var secondQueueTask = coordinator.QueueScanAsync(" OctoCat ", " Hello-World ");

        store.ReleasePendingSave();

        var queuedSnapshots = await Task.WhenAll(firstQueueTask, secondQueueTask);

        Assert.Single(queue.WorkItems);
        Assert.Equal(1, store.SavePendingCallCount);
        Assert.All(
            queuedSnapshots,
            snapshot => Assert.Equal(RepositoryScanStatuses.Pending, snapshot.Status));
    }

    [Fact]
    public async Task QueueScanAsync_ReturnsFreshCompletedSnapshotWithoutQueueing()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var legacyResultJson = JsonSerializer.Serialize(
            new
            {
                repositoryOwner = "octocat",
                repositoryName = "hello-world",
                pullRequestCount = 3,
                pullRequestsWithEmojiCount = 2,
                totalEmojiCount = 5,
                averageEmojisPerPullRequest = 1.67m,
                scannedAtUtc = utcNow.AddMinutes(-30),
            },
            SerializerOptions);
        var store = new FakeRepositoryScanStore
        {
            CurrentScan = new RepositoryScan
            {
                RepositoryOwner = "octocat",
                RepositoryName = "hello-world",
                NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
                Status = RepositoryScanStatuses.Completed,
                ResultJson = legacyResultJson,
                CreatedAtUtc = utcNow.UtcDateTime.AddHours(-1),
                UpdatedAtUtc = utcNow.UtcDateTime.AddMinutes(-30),
                CompletedAtUtc = utcNow.UtcDateTime.AddMinutes(-30),
                ExpiresAtUtc = utcNow.UtcDateTime.AddHours(23),
            },
        };
        var queue = new FakeRepositoryScanBackgroundQueue();
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var coordinator = new RepositoryScanCoordinator(
            queue,
            notifier,
            new FakeServiceScopeFactory(store),
            new FixedTimeProvider(utcNow));

        var snapshot = await coordinator.QueueScanAsync("octocat", "hello-world");

        Assert.Equal(RepositoryScanStatuses.Completed, snapshot.Status);
        Assert.NotNull(snapshot.Result);
        Assert.Equal(5, snapshot.Result.TotalEmojiCount);
        Assert.Equal(3, snapshot.Result.PullRequestSummary.ItemCount);
        Assert.Equal(0, snapshot.Result.IssueSummary.ItemCount);
        Assert.Equal(3, snapshot.TotalItemsRead);
        Assert.Equal(0, snapshot.IssuesRead);
        Assert.Empty(queue.WorkItems);
        Assert.Equal(0, store.SavePendingCallCount);
    }

    private sealed class FakeRepositoryScanStore : IRepositoryScanStore
    {
        private TaskCompletionSource pendingSaveCompletion = CreateCompletionSource();
        private bool blockPendingSave;

        public RepositoryScan? CurrentScan { get; set; }

        public int SavePendingCallCount { get; private set; }

        public TaskCompletionSource PendingSaveStarted { get; } = CreateCompletionSource();

        public void BlockPendingSave()
        {
            blockPendingSave = true;
        }

        public void ReleasePendingSave()
        {
            pendingSaveCompletion.TrySetResult();
        }

        public Task<RepositoryScan?> GetCurrentScanOrDeleteStaleAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentScan);

        public Task<RepositoryScan?> GetFreshScanOrDeleteStaleAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                CurrentScan is { Status: RepositoryScanStatuses.Completed }
                    ? CurrentScan
                    : null);

        public async Task<RepositoryScan> SavePendingScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            SavePendingCallCount++;
            CurrentScan = CreateScan(owner, repository, RepositoryScanStatuses.Pending);
            PendingSaveStarted.TrySetResult();

            if (blockPendingSave)
            {
                await pendingSaveCompletion.Task.WaitAsync(cancellationToken);
            }

            return CurrentScan;
        }

        public Task<RepositoryScan> SaveRunningScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            CurrentScan = CreateScan(owner, repository, RepositoryScanStatuses.Running);
            return Task.FromResult(CurrentScan);
        }

        public Task<RepositoryScan> SaveCompletedScanAsync(
            string owner,
            string repository,
            string resultJson,
            CancellationToken cancellationToken = default)
        {
            CurrentScan = CreateScan(owner, repository, RepositoryScanStatuses.Completed);
            CurrentScan.ResultJson = resultJson;
            CurrentScan.CompletedAtUtc = DateTime.UtcNow;
            CurrentScan.ExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            return Task.FromResult(CurrentScan);
        }

        public Task<RepositoryScan> SaveFailedScanAsync(
            string owner,
            string repository,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            CurrentScan = CreateScan(owner, repository, RepositoryScanStatuses.Failed);
            CurrentScan.FailureMessage = failureMessage;
            CurrentScan.CompletedAtUtc = DateTime.UtcNow;
            CurrentScan.ExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            return Task.FromResult(CurrentScan);
        }

        private static RepositoryScan CreateScan(string owner, string repository, string status) =>
            new()
            {
                RepositoryOwner = owner,
                RepositoryName = repository,
                NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
                Status = status,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };

        private static TaskCompletionSource CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FakeRepositoryScanBackgroundQueue : IRepositoryScanBackgroundQueue
    {
        public List<RepositoryScanWorkItem> WorkItems { get; } = [];

        public ValueTask QueueAsync(
            RepositoryScanWorkItem workItem,
            CancellationToken cancellationToken = default)
        {
            WorkItems.Add(workItem);
            return ValueTask.CompletedTask;
        }

        public ValueTask<RepositoryScanWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRepositoryScanProgressNotifier : IRepositoryScanProgressNotifier
    {
        private readonly Dictionary<string, RepositoryScanProgressUpdate> latestUpdates = [];

        public RepositoryScanProgressUpdate? GetLatest(string normalizedKey) =>
            latestUpdates.TryGetValue(normalizedKey, out var update)
                ? update
                : null;

        public RepositoryScanProgressSubscription Subscribe(string normalizedKey) =>
            throw new NotSupportedException();

        public void Store(RepositoryScanProgressUpdate update)
        {
            latestUpdates[update.NormalizedKey] = update;
        }

        public void Clear(string normalizedKey)
        {
            latestUpdates.Remove(normalizedKey);
        }

        public Task PublishAsync(
            RepositoryScanProgressUpdate update,
            CancellationToken cancellationToken = default)
        {
            latestUpdates[update.NormalizedKey] = update;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeServiceScopeFactory(IRepositoryScanStore scanStore) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(scanStore);
    }

    private sealed class FakeServiceScope(IRepositoryScanStore scanStore) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(scanStore);

        public void Dispose()
        {
        }
    }

    private sealed class FakeServiceProvider(IRepositoryScanStore scanStore) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRepositoryScanStore)
                ? scanStore
                : null;
    }
}
