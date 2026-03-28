using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanStoreTests
{
    [Fact]
    public async Task GetFreshScanOrDeleteStaleAsync_ReusesFreshCompletedScan()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = """{"emojiCount":42}""",
            CreatedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            UpdatedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            CompletedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            ExpiresAtUtc = utcNow.UtcDateTime.AddHours(22),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var scan = await testDatabase.Store.GetFreshScanOrDeleteStaleAsync("OctoCat", "Hello-World");

        Assert.NotNull(scan);
        Assert.Equal("""{"emojiCount":42}""", scan.ResultJson);
        Assert.Equal(1, await testDatabase.DbContext.RepositoryScans.CountAsync());
    }

    [Fact]
    public async Task GetFreshScanOrDeleteStaleAsync_DeletesCompletedScanAtExpirationBoundary()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = """{"emojiCount":42}""",
            CreatedAtUtc = utcNow.UtcDateTime.AddDays(-2),
            UpdatedAtUtc = utcNow.UtcDateTime.AddDays(-1),
            CompletedAtUtc = utcNow.UtcDateTime.AddDays(-1),
            ExpiresAtUtc = utcNow.UtcDateTime,
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var scan = await testDatabase.Store.GetFreshScanOrDeleteStaleAsync("octocat", "hello-world");

        Assert.Null(scan);
        Assert.Equal(0, await testDatabase.DbContext.RepositoryScans.CountAsync());
    }

    [Fact]
    public async Task GetFreshScanOrDeleteStaleAsync_DeletesAbandonedNonCompletedScanAfter24Hours()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Running,
            CreatedAtUtc = utcNow.UtcDateTime.AddDays(-2),
            UpdatedAtUtc = utcNow.UtcDateTime.AddDays(-2),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var scan = await testDatabase.Store.GetFreshScanOrDeleteStaleAsync("octocat", "hello-world");

        Assert.Null(scan);
        Assert.Equal(0, await testDatabase.DbContext.RepositoryScans.CountAsync());
    }

    [Fact]
    public async Task GetCurrentScanOrDeleteStaleAsync_ReturnsRunningScanBeforeExpiration()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Running,
            CreatedAtUtc = utcNow.UtcDateTime.AddMinutes(-30),
            UpdatedAtUtc = utcNow.UtcDateTime.AddMinutes(-5),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var scan = await testDatabase.Store.GetCurrentScanOrDeleteStaleAsync("octocat", "hello-world");

        Assert.NotNull(scan);
        Assert.Equal(RepositoryScanStatuses.Running, scan.Status);
    }

    [Fact]
    public async Task SavePendingAndRunningScanAsync_ClearTerminalFields()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = """{"emojiCount":42}""",
            FailureMessage = "Previous failure",
            CreatedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            UpdatedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            CompletedAtUtc = utcNow.UtcDateTime.AddHours(-2),
            ExpiresAtUtc = utcNow.UtcDateTime.AddHours(22),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var pendingScan = await testDatabase.Store.SavePendingScanAsync("octocat", "hello-world");

        Assert.Equal(RepositoryScanStatuses.Pending, pendingScan.Status);
        Assert.Null(pendingScan.ResultJson);
        Assert.Null(pendingScan.FailureMessage);
        Assert.Null(pendingScan.CompletedAtUtc);
        Assert.Null(pendingScan.ExpiresAtUtc);

        var runningScan = await testDatabase.Store.SaveRunningScanAsync("octocat", "hello-world");

        Assert.Equal(RepositoryScanStatuses.Running, runningScan.Status);
        Assert.Null(runningScan.ResultJson);
        Assert.Null(runningScan.FailureMessage);
        Assert.Null(runningScan.CompletedAtUtc);
        Assert.Null(runningScan.ExpiresAtUtc);
    }

    [Fact]
    public async Task SaveCompletedScanAsync_Sets24HourExpirationAndUpdatesExistingRecord()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        testDatabase.DbContext.RepositoryScans.Add(new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Pending,
            CreatedAtUtc = utcNow.UtcDateTime.AddMinutes(-5),
            UpdatedAtUtc = utcNow.UtcDateTime.AddMinutes(-5),
        });

        await testDatabase.DbContext.SaveChangesAsync();

        var savedScan = await testDatabase.Store.SaveCompletedScanAsync(
            " OctoCat ",
            " Hello-World ",
            """{"emojiCount":108}""");

        Assert.Equal(1, await testDatabase.DbContext.RepositoryScans.CountAsync());
        Assert.Equal("OctoCat", savedScan.RepositoryOwner);
        Assert.Equal("Hello-World", savedScan.RepositoryName);
        Assert.Equal(RepositoryScanStatuses.Completed, savedScan.Status);
        Assert.Equal("""{"emojiCount":108}""", savedScan.ResultJson);
        Assert.Equal(utcNow.UtcDateTime, savedScan.CompletedAtUtc);
        Assert.Equal(utcNow.UtcDateTime.AddHours(24), savedScan.ExpiresAtUtc);
    }

    [Fact]
    public async Task SaveFailedScanAsync_SetsFailureStateAndExpiration()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        await using var testDatabase = await RepositoryScanStoreTestDatabase.CreateAsync(utcNow);

        var savedScan = await testDatabase.Store.SaveFailedScanAsync(
            " OctoCat ",
            " Hello-World ",
            "GitHub timed out.");

        Assert.Equal("OctoCat", savedScan.RepositoryOwner);
        Assert.Equal("Hello-World", savedScan.RepositoryName);
        Assert.Equal(RepositoryScanStatuses.Failed, savedScan.Status);
        Assert.Null(savedScan.ResultJson);
        Assert.Equal("GitHub timed out.", savedScan.FailureMessage);
        Assert.Equal(utcNow.UtcDateTime, savedScan.CompletedAtUtc);
        Assert.Equal(utcNow.UtcDateTime.AddHours(24), savedScan.ExpiresAtUtc);
    }

    private sealed class RepositoryScanStoreTestDatabase : IAsyncDisposable
    {
        private RepositoryScanStoreTestDatabase(
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

        public static async Task<RepositoryScanStoreTestDatabase> CreateAsync(DateTimeOffset utcNow)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<EmojiEstimatorDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new EmojiEstimatorDbContext(options);
            await dbContext.Database.MigrateAsync();

            return new RepositoryScanStoreTestDatabase(
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
