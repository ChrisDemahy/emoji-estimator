using System.Collections.Concurrent;
using System.Text.Json;
using EmojiEstimator.PlaywrightTests.Infrastructure;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.Playwright;

namespace EmojiEstimator.PlaywrightTests;

[NonParallelizable]
public sealed class RepositoryJourneysTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private PlaywrightTestApplication application = null!;
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private IBrowserContext browserContext = null!;
    private IPage page = null!;

    [SetUp]
    public async Task SetUp()
    {
        application = await PlaywrightTestApplication.StartAsync();
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = application.BaseAddress.ToString(),
            Locale = "en-US",
            TimezoneId = "UTC"
        });
        page = await browserContext.NewPageAsync();
        page.Console += (_, message) => TestContext.Progress.WriteLine($"Browser console [{message.Type}]: {message.Text}");
        page.PageError += (_, message) => TestContext.Progress.WriteLine($"Browser page error: {message}");
        browserContext.RequestFailed += (_, request) =>
            TestContext.Progress.WriteLine($"Browser request failed: {request.Method} {request.Url} - {request.Failure}");
    }

    [TearDown]
    public async Task TearDown()
    {
        if (browserContext is not null)
        {
            await browserContext.CloseAsync();
        }

        if (browser is not null)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();

        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }

    [Test]
    public async Task HomePage_RendersExpectedProductMessaging()
    {
        await page.GotoAsync("/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await page.TitleAsync(), Does.Contain("Estimate repository signals"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-title")),
            Is.EqualTo("Estimate how many emojis and em dashes appear in a repository's pull requests and issues."));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-lead")),
            Does.Contain("EmojiEstimator scans public GitHub pull request descriptions and issue bodies"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code-accent")),
            Is.EqualTo("/{username}/{repository}"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code").Nth(0)),
            Is.EqualTo("/dotnet/aspnetcore"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code").Nth(1)),
            Is.EqualTo("/openclaw/clawhub"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code").Nth(2)),
            Is.EqualTo("/prisma/web"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code").Nth(3)),
            Is.EqualTo("/apache/superset"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".home-panel-code").Nth(4)),
            Is.EqualTo("/chroma-core/chroma"));
    }

    [Test]
    public async Task NavigatingToARepositoryPage_RendersTheRequestedRepository()
    {
        application.GitHubScenarios.SetScenario("dotnet", "aspnetcore", TestRepositoryScenario.Successful());

        await page.GotoAsync("/dotnet/aspnetcore", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await page.TitleAsync(), Does.Contain("dotnet/aspnetcore"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".repository-title span").Nth(0)),
            Is.EqualTo("dotnet"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".repository-title span").Nth(2)),
            Is.EqualTo("aspnetcore"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator(".repository-meta-card .app-inline-code")),
            Is.EqualTo("/dotnet/aspnetcore"));
    }

    [Test]
    public async Task RepositoryPage_LoadsImmediatelyAndStreamsLiveProgressUpdates()
    {
        var trackedRequests = new ConcurrentBag<string>();
        browserContext.Request += (_, request) =>
        {
            var path = new Uri(request.Url).AbsolutePath;
            if (path.Contains("live-updates", StringComparison.Ordinal) ||
                path.Contains("ensure-scan", StringComparison.Ordinal) ||
                path.Contains("repository-scans", StringComparison.Ordinal))
            {
                trackedRequests.Add($"{request.Method} {path}");
            }
        };

        application.Clock.SetUtcNow(FixedUtcNow);
        await application.SeedScanAsync(
            CreatePendingScan(
                "openclaw",
                "clawhub",
                updatedAtUtc: FixedUtcNow.AddSeconds(-15)));
        application.GitHubScenarios.SetScenario(
            "openclaw",
            "clawhub",
            TestRepositoryScenario.Successful(
                new TestContentPage(
                    TimeSpan.FromMilliseconds(2000),
                    GitHubContentItem.CreatePullRequest(1, "🎉 —"),
                    GitHubContentItem.CreatePullRequest(2, "Ship it 🚀")),
                new TestContentPage(
                    TimeSpan.FromMilliseconds(2000),
                    GitHubContentItem.CreateIssue(3, "Needs docs —"),
                    GitHubContentItem.CreateIssue(4, "Nice catch 🎯 —"))));

        await page.GotoAsync("/openclaw/clawhub", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='status-badge']")), Is.EqualTo("Queued"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='state-title']")), Is.EqualTo("Preparing repository scan"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-item-count']")), Is.EqualTo("—"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator("[data-role='source-note']")),
            Is.EqualTo("This page updates live as the scan progresses."));

        await WaitForTextAsync(page.Locator("[data-role='connection-badge']"), "Live updates connected");
        await WaitForTextAsync(
            page.Locator("[data-role='source-note']"),
            "This page updates live as the scan progresses.");

        await WaitForTextAsync(page.Locator("[data-role='status-badge']"), "Completed");
        await WaitForTextAsync(
            page.Locator("[data-role='state-message']"),
            "Scan completed after processing 2 pull requests and 2 issues.");

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='pull-requests-read']")), Is.EqualTo("2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issues-read']")), Is.EqualTo("2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='total-items-read']")), Is.EqualTo("4"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-item-count']")), Is.EqualTo("4"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-emoji-count']")), Is.EqualTo("3"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-em-dash-count']")), Is.EqualTo("3"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='pull-request-item-count']")), Is.EqualTo("2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='pull-request-average-em-dashes']")), Is.EqualTo("0.5"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-item-count']")), Is.EqualTo("2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-total-em-dash-count']")), Is.EqualTo("2"));
        Assert.That(
            await GetNormalizedTextAsync(page.Locator("[data-role='source-note']")),
            Is.EqualTo("Showing the latest completed scan result."));
        Assert.That(trackedRequests, Has.Some.Contains("GET /openclaw/clawhub/live-updates"));
        Assert.That(trackedRequests, Has.Some.Contains("POST /openclaw/clawhub/ensure-scan"));
        Assert.That(trackedRequests, Has.None.Contains("/hubs/repository-scans"));
    }

    [Test]
    public async Task RepositoryPage_ShowsFreshCachedResultsImmediatelyWithoutRescanning()
    {
        application.Clock.SetUtcNow(FixedUtcNow);
        await application.SeedScanAsync(
            CreateCompletedScan(
                "prisma",
                "web",
                CreateSummary(itemCount: 4, itemsWithEmojiCount: 3, totalEmojiCount: 10, itemsWithEmDashCount: 2, totalEmDashCount: 4),
                CreateSummary(itemCount: 6, itemsWithEmojiCount: 4, totalEmojiCount: 9, itemsWithEmDashCount: 3, totalEmDashCount: 6),
                completedAtUtc: FixedUtcNow.AddMinutes(-30),
                expiresAtUtc: FixedUtcNow.AddHours(6)));

        await page.GotoAsync("/prisma/web", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='status-badge']")), Is.EqualTo("Completed"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-item-count']")), Is.EqualTo("10"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-average-emojis']")), Is.EqualTo("1.9"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='pull-request-total-em-dash-count']")), Is.EqualTo("4"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-average-em-dashes']")), Is.EqualTo("1"));

        await WaitForTextAsync(page.Locator("[data-role='connection-badge']"), "Live updates connected");
        await Task.Delay(250);

        Assert.That(application.GitHubScenarios.GetInvocationCount("prisma", "web"), Is.Zero);
    }

    [Test]
    public async Task RepositoryPage_RendersLegacyCachedResultsWithoutRequiringANewScan()
    {
        application.Clock.SetUtcNow(FixedUtcNow);
        await application.SeedScanAsync(
            CreateLegacyCompletedScan(
                "apache",
                "superset",
                completedAtUtc: FixedUtcNow.AddMinutes(-20),
                expiresAtUtc: FixedUtcNow.AddHours(4)));

        await page.GotoAsync("/apache/superset", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='status-badge']")), Is.EqualTo("Completed"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-item-count']")), Is.EqualTo("2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-emoji-count']")), Is.EqualTo("3"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-em-dash-count']")), Is.EqualTo("0"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='pull-request-average-emojis']")), Is.EqualTo("1.5"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-item-count']")), Is.EqualTo("0"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-total-em-dash-count']")), Is.EqualTo("0"));

        await WaitForTextAsync(page.Locator("[data-role='connection-badge']"), "Live updates connected");
        Assert.That(application.GitHubScenarios.GetInvocationCount("apache", "superset"), Is.Zero);
    }

    [Test]
    public async Task RepositoryPage_RemovesStaleResultsAndRescans()
    {
        application.Clock.SetUtcNow(FixedUtcNow);
        await application.SeedScanAsync(
            CreateCompletedScan(
                "chroma-core",
                "chroma",
                CreateSummary(itemCount: 8, itemsWithEmojiCount: 8, totalEmojiCount: 80, itemsWithEmDashCount: 4, totalEmDashCount: 12),
                CreateSummary(itemCount: 2, itemsWithEmojiCount: 1, totalEmojiCount: 2, itemsWithEmDashCount: 1, totalEmDashCount: 1),
                completedAtUtc: FixedUtcNow.AddDays(-1),
                expiresAtUtc: FixedUtcNow));
        application.GitHubScenarios.SetScenario(
            "chroma-core",
            "chroma",
            TestRepositoryScenario.Successful(
                new TestContentPage(
                    TimeSpan.FromMilliseconds(250),
                    GitHubContentItem.CreatePullRequest(1, "🎉"),
                    GitHubContentItem.CreatePullRequest(2, "🚀🎉"),
                    GitHubContentItem.CreateIssue(3, "Follow-up —"))));

        await page.GotoAsync("/chroma-core/chroma", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-average-emojis']")), Is.Not.EqualTo("8.2"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-emoji-count']")), Is.Not.EqualTo("82"));

        await WaitForTextAsync(page.Locator("[data-role='status-badge']"), "Completed");

        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-average-emojis']")), Is.EqualTo("1"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-emoji-count']")), Is.EqualTo("3"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='repository-total-em-dash-count']")), Is.EqualTo("1"));
        Assert.That(await GetNormalizedTextAsync(page.Locator("[data-role='issue-item-count']")), Is.EqualTo("1"));
        Assert.That(application.GitHubScenarios.GetInvocationCount("chroma-core", "chroma"), Is.EqualTo(1));

        var savedScan = await WaitForScanAsync(
            application,
            "chroma-core",
            "chroma",
            scan => string.Equals(scan.Status, RepositoryScanStatuses.Completed, StringComparison.Ordinal) &&
                    scan.ResultJson is not null &&
                    scan.ResultJson.Contains("\"totalEmojiCount\":3", StringComparison.Ordinal));
        Assert.That(savedScan.Status, Is.EqualTo(RepositoryScanStatuses.Completed));
        Assert.That(savedScan.ResultJson, Does.Contain("\"totalEmojiCount\":3"));
    }

    [Test]
    public async Task RepositoryPage_ShowsAFailureStateWhenTheScanFails()
    {
        application.Clock.SetUtcNow(FixedUtcNow);
        application.GitHubScenarios.SetScenario(
            "chroma-core",
            "chroma",
            TestRepositoryScenario.Failed(
                "Repository not found.",
                TimeSpan.FromMilliseconds(250)));
        await application.SeedScanAsync(
            CreateFailedScan(
                "chroma-core",
                "chroma",
                "Repository not found.",
                completedAtUtc: FixedUtcNow.AddMinutes(-5),
                expiresAtUtc: FixedUtcNow.AddHours(6)));

        await page.GotoAsync("/chroma-core/chroma", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        await WaitForTextAsync(page.Locator("[data-role='status-badge']"), "Failed");
        await WaitForTextAsync(page.Locator("[data-role='state-title']"), "Scan failed");
        await WaitForTextAsync(page.Locator("[data-role='failure-message']"), "Repository not found.");
        await WaitForClassToExcludeTextAsync(page.Locator("[data-role='failure-panel']"), "hidden");

        var failurePanelClasses = await page.Locator("[data-role='failure-panel']").GetAttributeAsync("class") ?? string.Empty;
        Assert.That(failurePanelClasses, Does.Not.Contain("hidden"));
        await WaitForTextAsync(
            page.Locator("[data-role='source-note']"),
            "The latest scan ended with an error.");
    }

    private static RepositoryScan CreateCompletedScan(
        string owner,
        string repository,
        RepositoryContentSummary pullRequestSummary,
        RepositoryContentSummary issueSummary,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        var repositorySummary = RepositoryContentSummary.Combine(pullRequestSummary, issueSummary);
        var result = new RepositoryScanResult
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            PullRequestCount = pullRequestSummary.ItemCount,
            PullRequestsWithEmojiCount = pullRequestSummary.ItemsWithEmojiCount,
            TotalEmojiCount = pullRequestSummary.TotalEmojiCount,
            AverageEmojisPerPullRequest = pullRequestSummary.AverageEmojisPerItem,
            PullRequestSummary = pullRequestSummary,
            IssueSummary = issueSummary,
            RepositorySummary = repositorySummary,
            ScannedAtUtc = completedAtUtc
        };

        return new RepositoryScan
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = JsonSerializer.Serialize(result, SerializerOptions),
            CreatedAtUtc = completedAtUtc.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = completedAtUtc.UtcDateTime,
            CompletedAtUtc = completedAtUtc.UtcDateTime,
            ExpiresAtUtc = expiresAtUtc.UtcDateTime
        };
    }

    private static RepositoryContentSummary CreateSummary(
        int itemCount,
        int itemsWithEmojiCount,
        int totalEmojiCount,
        int itemsWithEmDashCount,
        int totalEmDashCount) =>
        RepositoryContentSummary.Create(
            itemCount,
            itemsWithEmojiCount,
            totalEmojiCount,
            itemsWithEmDashCount,
            totalEmDashCount);

    private static RepositoryScan CreatePendingScan(
        string owner,
        string repository,
        DateTimeOffset updatedAtUtc) =>
        new()
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Pending,
            CreatedAtUtc = updatedAtUtc.UtcDateTime.AddMinutes(-5),
            UpdatedAtUtc = updatedAtUtc.UtcDateTime,
            CompletedAtUtc = null,
            ExpiresAtUtc = null
        };

    private static RepositoryScan CreateLegacyCompletedScan(
        string owner,
        string repository,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        var legacyJson = JsonSerializer.Serialize(
            new
            {
                repositoryOwner = owner,
                repositoryName = repository,
                pullRequestCount = 2,
                pullRequestsWithEmojiCount = 1,
                totalEmojiCount = 3,
                averageEmojisPerPullRequest = 1.5m,
                scannedAtUtc = completedAtUtc
            },
            SerializerOptions);

        return new RepositoryScan
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = legacyJson,
            CreatedAtUtc = completedAtUtc.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = completedAtUtc.UtcDateTime,
            CompletedAtUtc = completedAtUtc.UtcDateTime,
            ExpiresAtUtc = expiresAtUtc.UtcDateTime
        };
    }

    private static RepositoryScan CreateFailedScan(
        string owner,
        string repository,
        string failureMessage,
        DateTimeOffset completedAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new()
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Failed,
            FailureMessage = failureMessage,
            CreatedAtUtc = completedAtUtc.UtcDateTime.AddHours(-1),
            UpdatedAtUtc = completedAtUtc.UtcDateTime,
            CompletedAtUtc = completedAtUtc.UtcDateTime,
            ExpiresAtUtc = expiresAtUtc.UtcDateTime
        };

    private static async Task WaitForTextAsync(
        ILocator locator,
        string expectedText,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(10));

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (string.Equals(await TryGetNormalizedTextAsync(locator), expectedText, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Timed out waiting for text '{expectedText}'. Last value: '{await TryGetNormalizedTextAsync(locator)}'.");
    }

    private static async Task<RepositoryScan> WaitForScanAsync(
        PlaywrightTestApplication application,
        string owner,
        string repository,
        Func<RepositoryScan, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(10));

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var scan = await application.GetRequiredScanAsync(owner, repository);
                if (predicate(scan))
                {
                    return scan;
                }
            }
            catch (InvalidOperationException)
            {
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Timed out waiting for repository scan state for {owner}/{repository}.");
        return null!;
    }

    private static async Task<string> GetNormalizedTextAsync(ILocator locator)
    {
        var text = await locator.TextContentAsync() ?? string.Empty;
        return NormalizeWhitespace(text);
    }

    private static async Task<string> TryGetNormalizedTextAsync(ILocator locator)
    {
        try
        {
            return await GetNormalizedTextAsync(locator);
        }
        catch (PlaywrightException)
        {
            return string.Empty;
        }
    }

    private static async Task WaitForClassToExcludeTextAsync(
        ILocator locator,
        string excludedText,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(10));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var classValue = await locator.GetAttributeAsync("class") ?? string.Empty;
            if (!classValue.Contains(excludedText, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"Timed out waiting for class to exclude '{excludedText}'. Last value: '{await locator.GetAttributeAsync("class") ?? string.Empty}'.");
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
