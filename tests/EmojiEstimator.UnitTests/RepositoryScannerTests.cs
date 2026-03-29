using System.Text.Json;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScannerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ScanAsync_AggregatesPullRequestsAndPersistsCompletedResult()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var reader = new FakeGitHubContentReader(
        [
            GitHubContentItem.CreatePullRequest(1, "🎉🎉 —"),
            GitHubContentItem.CreatePullRequest(2, null),
            GitHubContentItem.CreatePullRequest(3, "Ship it 👍🏽 —"),
            GitHubContentItem.CreateIssue(4, "Needs follow-up —"),
        ]);
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            reader,
            CreateAggregator(utcNow),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync(" OctoCat ", " Hello-World ");

        Assert.Equal(1, reader.CallCount);
        Assert.Equal("OctoCat", result.RepositoryOwner);
        Assert.Equal("Hello-World", result.RepositoryName);
        Assert.Equal(3, result.PullRequestCount);
        Assert.Equal(2, result.PullRequestsWithEmojiCount);
        Assert.Equal(3, result.TotalEmojiCount);
        Assert.Equal(1m, result.AverageEmojisPerPullRequest);
        Assert.Equal(3, result.PullRequestSummary.ItemCount);
        Assert.Equal(2, result.PullRequestSummary.TotalEmDashCount);
        Assert.Equal(1, result.IssueSummary.ItemCount);
        Assert.Equal(1, result.IssueSummary.TotalEmDashCount);
        Assert.Equal(4, result.RepositorySummary.ItemCount);
        Assert.Equal(3, result.RepositorySummary.TotalEmDashCount);

        var savedScan = await testDatabase.DbContext.RepositoryScans.SingleAsync();
        Assert.Equal(RepositoryScanStatuses.Completed, savedScan.Status);

        var persistedResult = JsonSerializer.Deserialize<RepositoryScanResult>(savedScan.ResultJson!, SerializerOptions);
        Assert.NotNull(persistedResult);
        Assert.Equal(result.TotalEmojiCount, persistedResult.TotalEmojiCount);
        Assert.Equal(result.PullRequestCount, persistedResult.PullRequestCount);
        Assert.Equal(result.PullRequestsWithEmojiCount, persistedResult.PullRequestsWithEmojiCount);
        Assert.Equal(result.AverageEmojisPerPullRequest, persistedResult.AverageEmojisPerPullRequest);
        Assert.Equal(result.IssueSummary.ItemCount, persistedResult.IssueSummary.ItemCount);
        Assert.Equal(result.RepositorySummary.TotalEmDashCount, persistedResult.RepositorySummary.TotalEmDashCount);
    }

    [Fact]
    public async Task ScanAsync_ReusesFreshPersistedResultWithoutCallingGitHub()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var legacyResultJson = JsonSerializer.Serialize(
            new
            {
                repositoryOwner = "octocat",
                repositoryName = "hello-world",
                pullRequestCount = 5,
                pullRequestsWithEmojiCount = 3,
                totalEmojiCount = 8,
                averageEmojisPerPullRequest = 1.6m,
                scannedAtUtc = utcNow.AddHours(-1),
            },
            SerializerOptions);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = legacyResultJson,
            CreatedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            CompletedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            ExpiresAtUtc = utcNow.UtcDateTime.AddHours(23),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var reader = new FakeGitHubContentReader(Array.Empty<GitHubContentItem>(), throwOnCall: true);
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            reader,
            CreateAggregator(utcNow),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync("OctoCat", "Hello-World");

        Assert.Equal(0, reader.CallCount);
        Assert.Equal(8, result.TotalEmojiCount);
        Assert.Equal(5, result.PullRequestCount);
        Assert.Equal(1.6m, result.AverageEmojisPerPullRequest);
        Assert.Equal(5, result.PullRequestSummary.ItemCount);
        Assert.Equal(0, result.IssueSummary.ItemCount);
        Assert.Equal(5, result.RepositorySummary.ItemCount);
    }

    [Fact]
    public async Task ScanAsync_PersistsZeroPullRequestRepositories()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubContentReader(Array.Empty<GitHubContentItem>()),
            CreateAggregator(utcNow),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync("octocat", "empty-repository");

        Assert.Equal(0, result.PullRequestCount);
        Assert.Equal(0, result.PullRequestsWithEmojiCount);
        Assert.Equal(0, result.TotalEmojiCount);
        Assert.Equal(0m, result.AverageEmojisPerPullRequest);
        Assert.Equal(0, result.RepositorySummary.ItemCount);
        Assert.Equal(1, await testDatabase.DbContext.RepositoryScans.CountAsync());
    }

    [Fact]
    public async Task ScanAsync_PublishesPullRequestAndIssueProgressUpdates()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubContentReader(
            [
                GitHubContentItem.CreatePullRequest(1, "🎉"),
                GitHubContentItem.CreateIssue(2, "🚀 —"),
            ],
            progressUpdates:
            [
                new GitHubContentReadProgress(GitHubContentKind.PullRequest, 1, 1, 1, 0),
                new GitHubContentReadProgress(GitHubContentKind.Issue, 1, 1, 1, 1),
            ]),
            CreateAggregator(utcNow),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync("octocat", "hello-world");

        Assert.Contains(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Running &&
                update.CurrentPageNumber is null &&
                update.PullRequestsRead is null);
        Assert.Contains(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Running &&
                update.CurrentPageNumber == 1 &&
                update.CurrentContentKind == GitHubContentKind.PullRequest &&
                update.CurrentPageItemCount == 1 &&
                update.PullRequestsRead == 1 &&
                update.IssuesRead == 0 &&
                update.TotalItemsRead == 1 &&
                update.Message == "Fetched pull request page 1.");
        Assert.Contains(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Running &&
                update.CurrentPageNumber == 1 &&
                update.CurrentContentKind == GitHubContentKind.Issue &&
                update.CurrentPageItemCount == 1 &&
                update.PullRequestsRead == 1 &&
                update.IssuesRead == 1 &&
                update.TotalItemsRead == 2 &&
                update.Message == "Fetched issue page 1.");

        var completedUpdate = Assert.Single(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Completed);
        Assert.NotNull(completedUpdate.Result);
        Assert.Equal(result.PullRequestCount, completedUpdate.Result.PullRequestCount);
        Assert.Equal(result.TotalEmojiCount, completedUpdate.Result.TotalEmojiCount);
        Assert.Equal(result.IssueSummary.ItemCount, completedUpdate.IssuesRead);
        Assert.Equal(result.RepositorySummary.ItemCount, completedUpdate.TotalItemsRead);
    }

    [Fact]
    public async Task ScanAsync_PersistsFailureAndPublishesFailedUpdate()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubContentReader(Array.Empty<GitHubContentItem>(), throwOnCall: true),
            CreateAggregator(utcNow),
            notifier,
            new FixedTimeProvider(utcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scanner.ScanAsync("octocat", "broken-repository"));

        var failedScan = await testDatabase.DbContext.RepositoryScans.SingleAsync();
        Assert.Equal(RepositoryScanStatuses.Failed, failedScan.Status);
        Assert.Equal("GitHub should not be queried for a fresh cached scan.", failedScan.FailureMessage);

        var failedUpdate = Assert.Single(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Failed);
        Assert.Equal(failedScan.FailureMessage, failedUpdate.FailureMessage);
    }

    [Fact]
    public async Task ScanAsync_DeduplicatesConcurrentRepositoryRequests()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var store = new InMemoryRepositoryScanStore();
        var reader = new DelayedGitHubContentReader(
        [
            GitHubContentItem.CreatePullRequest(1, "🎉"),
        ]);
        var aggregator = CreateAggregator(utcNow);
        var firstScanner = new RepositoryScanner(
            store,
            reader,
            aggregator,
            new RecordingRepositoryScanProgressNotifier(),
            new FixedTimeProvider(utcNow));
        var secondScanner = new RepositoryScanner(
            store,
            reader,
            aggregator,
            new RecordingRepositoryScanProgressNotifier(),
            new FixedTimeProvider(utcNow));

        var firstScanTask = firstScanner.ScanAsync("octocat", "concurrent-repository");
        var secondScanTask = secondScanner.ScanAsync("octocat", "concurrent-repository");

        var results = await Task.WhenAll(firstScanTask, secondScanTask);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, store.SaveCompletedCallCount);
        Assert.Equal(results[0].TotalEmojiCount, results[1].TotalEmojiCount);
        Assert.Equal(results[0].PullRequestCount, results[1].PullRequestCount);
    }

    private sealed class FakeGitHubContentReader(
        IReadOnlyList<GitHubContentItem> contentItems,
        bool throwOnCall = false,
        IReadOnlyList<GitHubContentReadProgress>? progressUpdates = null) : IGitHubContentReader
    {
        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<GitHubContentItem>> ReadAllAsync(
            string owner,
            string repository,
            Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (throwOnCall)
            {
                throw new InvalidOperationException("GitHub should not be queried for a fresh cached scan.");
            }

            if (progressCallback is not null && progressUpdates is not null)
            {
                foreach (var progressUpdate in progressUpdates)
                {
                    await progressCallback(progressUpdate, cancellationToken);
                }
            }

            return contentItems;
        }
    }

    private sealed class DelayedGitHubContentReader(IReadOnlyList<GitHubContentItem> contentItems) : IGitHubContentReader
    {
        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<GitHubContentItem>> ReadAllAsync(
            string owner,
            string repository,
            Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            return contentItems;
        }
    }

    private sealed class InMemoryRepositoryScanStore : IRepositoryScanStore
    {
        private readonly Lock syncLock = new();
        private RepositoryScan? currentScan;

        public int SaveCompletedCallCount { get; private set; }

        public Task<RepositoryScan?> GetCurrentScanOrDeleteStaleAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                return Task.FromResult(currentScan);
            }
        }

        public Task<RepositoryScan?> GetFreshScanOrDeleteStaleAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                return Task.FromResult(
                    currentScan is { Status: RepositoryScanStatuses.Completed }
                        ? currentScan
                        : null);
            }
        }

        public Task<RepositoryScan> SavePendingScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                currentScan = CreateScan(owner, repository, RepositoryScanStatuses.Pending);
                return Task.FromResult(currentScan);
            }
        }

        public Task<RepositoryScan> SaveRunningScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                currentScan = CreateScan(owner, repository, RepositoryScanStatuses.Running);
                return Task.FromResult(currentScan);
            }
        }

        public Task<RepositoryScan> SaveCompletedScanAsync(
            string owner,
            string repository,
            string resultJson,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                SaveCompletedCallCount++;
                currentScan = CreateScan(owner, repository, RepositoryScanStatuses.Completed);
                currentScan.ResultJson = resultJson;
                currentScan.CompletedAtUtc = DateTime.UtcNow;
                currentScan.ExpiresAtUtc = DateTime.UtcNow.AddHours(1);
                return Task.FromResult(currentScan);
            }
        }

        public Task<RepositoryScan> SaveFailedScanAsync(
            string owner,
            string repository,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                currentScan = CreateScan(owner, repository, RepositoryScanStatuses.Failed);
                currentScan.FailureMessage = failureMessage;
                currentScan.CompletedAtUtc = DateTime.UtcNow;
                currentScan.ExpiresAtUtc = DateTime.UtcNow.AddHours(1);
                return Task.FromResult(currentScan);
            }
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
    }

    private sealed class RepositoryScannerTestDatabase : IAsyncDisposable
    {
        private RepositoryScannerTestDatabase(
            SqliteConnection connection,
            EmojiEstimatorDbContext dbContext,
            RepositoryScanStore store)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
        }

        public SqliteConnection Connection { get; }

        public EmojiEstimatorDbContext DbContext { get; }

        public RepositoryScanStore Store { get; }

        public static async Task<RepositoryScannerTestDatabase> CreateAsync(DateTimeOffset utcNow)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<EmojiEstimatorDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new EmojiEstimatorDbContext(options);
            await dbContext.Database.MigrateAsync();

            return new RepositoryScannerTestDatabase(
                connection,
                dbContext,
                new RepositoryScanStore(dbContext, new FixedTimeProvider(utcNow)));
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class RecordingRepositoryScanProgressNotifier : IRepositoryScanProgressNotifier
    {
        private readonly Lock syncLock = new();
        private readonly Dictionary<string, RepositoryScanProgressUpdate> latestUpdates = [];

        public List<RepositoryScanProgressUpdate> Updates { get; } = [];

        public RepositoryScanProgressUpdate? GetLatest(string normalizedKey)
        {
            lock (syncLock)
            {
                return latestUpdates.TryGetValue(normalizedKey, out var update)
                    ? update
                    : null;
            }
        }

        public RepositoryScanProgressSubscription Subscribe(string normalizedKey) =>
            throw new NotSupportedException();

        public void Store(RepositoryScanProgressUpdate update)
        {
            lock (syncLock)
            {
                latestUpdates[update.NormalizedKey] = update;
            }
        }

        public void Clear(string normalizedKey)
        {
            lock (syncLock)
            {
                latestUpdates.Remove(normalizedKey);
            }
        }

        public Task PublishAsync(
            RepositoryScanProgressUpdate update,
            CancellationToken cancellationToken = default)
        {
            lock (syncLock)
            {
                latestUpdates[update.NormalizedKey] = update;
                Updates.Add(update);
            }

            return Task.CompletedTask;
        }
    }

    private static RepositoryScanAggregator CreateAggregator(DateTimeOffset utcNow) =>
        new(
            new UnicodeEmojiCounter(),
            new CanonicalEmDashCounter(),
            new FixedTimeProvider(utcNow));
}
