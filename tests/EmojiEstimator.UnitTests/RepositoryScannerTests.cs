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
        var reader = new FakeGitHubPullRequestReader(
        [
            new GitHubPullRequestBody(1, "🎉🎉"),
            new GitHubPullRequestBody(2, null),
            new GitHubPullRequestBody(3, "Ship it 👍🏽"),
        ]);
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            reader,
            new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow)),
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

        var savedScan = await testDatabase.DbContext.RepositoryScans.SingleAsync();
        Assert.Equal(RepositoryScanStatuses.Completed, savedScan.Status);

        var persistedResult = JsonSerializer.Deserialize<RepositoryScanResult>(savedScan.ResultJson!, SerializerOptions);
        Assert.NotNull(persistedResult);
        Assert.Equal(result.TotalEmojiCount, persistedResult.TotalEmojiCount);
        Assert.Equal(result.PullRequestCount, persistedResult.PullRequestCount);
        Assert.Equal(result.PullRequestsWithEmojiCount, persistedResult.PullRequestsWithEmojiCount);
        Assert.Equal(result.AverageEmojisPerPullRequest, persistedResult.AverageEmojisPerPullRequest);
    }

    [Fact]
    public async Task ScanAsync_ReusesFreshPersistedResultWithoutCallingGitHub()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var persistedResult = new RepositoryScanResult
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            PullRequestCount = 5,
            PullRequestsWithEmojiCount = 3,
            TotalEmojiCount = 8,
            AverageEmojisPerPullRequest = 1.6m,
            ScannedAtUtc = utcNow.AddHours(-1),
        };

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = JsonSerializer.Serialize(persistedResult, SerializerOptions),
            CreatedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            CompletedAtUtc = utcNow.UtcDateTime.AddHours(-1),
            ExpiresAtUtc = utcNow.UtcDateTime.AddHours(23),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var reader = new FakeGitHubPullRequestReader(Array.Empty<GitHubPullRequestBody>(), throwOnCall: true);
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            reader,
            new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow)),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync("OctoCat", "Hello-World");

        Assert.Equal(0, reader.CallCount);
        Assert.Equal(persistedResult.TotalEmojiCount, result.TotalEmojiCount);
        Assert.Equal(persistedResult.PullRequestCount, result.PullRequestCount);
        Assert.Equal(persistedResult.AverageEmojisPerPullRequest, result.AverageEmojisPerPullRequest);
    }

    [Fact]
    public async Task ScanAsync_PersistsZeroPullRequestRepositories()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubPullRequestReader(Array.Empty<GitHubPullRequestBody>()),
            new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow)),
            notifier,
            new FixedTimeProvider(utcNow));

        var result = await scanner.ScanAsync("octocat", "empty-repository");

        Assert.Equal(0, result.PullRequestCount);
        Assert.Equal(0, result.PullRequestsWithEmojiCount);
        Assert.Equal(0, result.TotalEmojiCount);
        Assert.Equal(0m, result.AverageEmojisPerPullRequest);
        Assert.Equal(1, await testDatabase.DbContext.RepositoryScans.CountAsync());
    }

    [Fact]
    public async Task ScanAsync_PublishesProgressAndCompletionUpdates()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubPullRequestReader(
            [
                new GitHubPullRequestBody(1, "🎉"),
                new GitHubPullRequestBody(2, "🚀"),
            ],
            progressUpdates:
            [
                new GitHubPullRequestReadProgress(1, 2, 2),
            ]),
            new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow)),
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
                update.PullRequestsRead == 2);

        var completedUpdate = Assert.Single(
            notifier.Updates,
            update => update.Status == RepositoryScanStatuses.Completed);
        Assert.NotNull(completedUpdate.Result);
        Assert.Equal(result.TotalEmojiCount, completedUpdate.Result.TotalEmojiCount);
    }

    [Fact]
    public async Task ScanAsync_PersistsFailureAndPublishesFailedUpdate()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScannerTestDatabase.CreateAsync(utcNow);
        var notifier = new RecordingRepositoryScanProgressNotifier();
        var scanner = new RepositoryScanner(
            testDatabase.Store,
            new FakeGitHubPullRequestReader(Array.Empty<GitHubPullRequestBody>(), throwOnCall: true),
            new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow)),
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
        var reader = new DelayedGitHubPullRequestReader(
        [
            new GitHubPullRequestBody(1, "🎉"),
        ]);
        var aggregator = new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow));
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

    private sealed class FakeGitHubPullRequestReader(
        IReadOnlyList<GitHubPullRequestBody> pullRequests,
        bool throwOnCall = false,
        IReadOnlyList<GitHubPullRequestReadProgress>? progressUpdates = null) : IGitHubPullRequestReader
    {
        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<GitHubPullRequestBody>> ReadAllAsync(
            string owner,
            string repository,
            Func<GitHubPullRequestReadProgress, CancellationToken, ValueTask>? progressCallback = null,
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

            return pullRequests;
        }
    }

    private sealed class DelayedGitHubPullRequestReader(IReadOnlyList<GitHubPullRequestBody> pullRequests) : IGitHubPullRequestReader
    {
        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<GitHubPullRequestBody>> ReadAllAsync(
            string owner,
            string repository,
            Func<GitHubPullRequestReadProgress, CancellationToken, ValueTask>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            return pullRequests;
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
}
