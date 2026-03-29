using System.Collections.Concurrent;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;

namespace EmojiEstimator.PlaywrightTests.Infrastructure;

public sealed class TestGitHubScenarioStore
{
    private readonly ConcurrentDictionary<string, TestRepositoryScenario> scenarios = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> invocationCounts = new(StringComparer.Ordinal);

    public void SetScenario(string owner, string repository, TestRepositoryScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var normalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository);
        scenarios[normalizedKey] = scenario;
        invocationCounts[normalizedKey] = 0;
    }

    public int GetInvocationCount(string owner, string repository)
    {
        var normalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository);
        return invocationCounts.TryGetValue(normalizedKey, out var count)
            ? count
            : 0;
    }

    public TestRepositoryScenario GetRequiredScenario(string owner, string repository)
    {
        var normalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository);

        return scenarios.TryGetValue(normalizedKey, out var scenario)
            ? scenario
            : throw new InvalidOperationException($"No fake GitHub scenario is configured for {owner}/{repository}.");
    }

    public void RecordInvocation(string owner, string repository)
    {
        var normalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository);
        invocationCounts.AddOrUpdate(normalizedKey, 1, static (_, currentCount) => currentCount + 1);
    }
}

public sealed class TestRepositoryScenario
{
    public TestRepositoryScenario(
        IReadOnlyList<TestContentPage> pages,
        string? failureMessage = null,
        TimeSpan? failureDelay = null)
    {
        Pages = pages;
        FailureMessage = failureMessage;
        FailureDelay = failureDelay ?? TimeSpan.Zero;
    }

    public IReadOnlyList<TestContentPage> Pages { get; }

    public string? FailureMessage { get; }

    public TimeSpan FailureDelay { get; }

    public static TestRepositoryScenario Successful(params TestContentPage[] pages) =>
        new(pages);

    public static TestRepositoryScenario Failed(string failureMessage, TimeSpan? failureDelay = null) =>
        new(Array.Empty<TestContentPage>(), failureMessage, failureDelay);
}

public sealed class TestContentPage
{
    public TestContentPage(TimeSpan delay, params GitHubContentItem[] contentItems)
    {
        Delay = delay;
        ContentItems = contentItems;
    }

    public TimeSpan Delay { get; }

    public IReadOnlyList<GitHubContentItem> ContentItems { get; }
}

public sealed class TestGitHubContentReader(TestGitHubScenarioStore scenarioStore) : IGitHubContentReader
{
    public async Task<IReadOnlyList<GitHubContentItem>> ReadAllAsync(
        string owner,
        string repository,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var scenario = scenarioStore.GetRequiredScenario(owner, repository);
        scenarioStore.RecordInvocation(owner, repository);

        if (!string.IsNullOrWhiteSpace(scenario.FailureMessage))
        {
            if (scenario.FailureDelay > TimeSpan.Zero)
            {
                await Task.Delay(scenario.FailureDelay, cancellationToken);
            }

            throw new InvalidOperationException(scenario.FailureMessage);
        }

        var contentItems = new List<GitHubContentItem>();
        var pullRequestsRead = 0;
        var issuesRead = 0;

        for (var pageIndex = 0; pageIndex < scenario.Pages.Count; pageIndex++)
        {
            var page = scenario.Pages[pageIndex];

            if (page.Delay > TimeSpan.Zero)
            {
                await Task.Delay(page.Delay, cancellationToken);
            }

            contentItems.AddRange(page.ContentItems);
            pullRequestsRead += page.ContentItems.Count(contentItem => contentItem.Kind == GitHubContentKind.PullRequest);
            issuesRead += page.ContentItems.Count(contentItem => contentItem.Kind == GitHubContentKind.Issue);

            if (progressCallback is not null)
            {
                await progressCallback(
                    new GitHubContentReadProgress(
                        page.ContentItems.FirstOrDefault()?.Kind ?? GitHubContentKind.PullRequest,
                        pageIndex + 1,
                        page.ContentItems.Count,
                        pullRequestsRead,
                        issuesRead),
                    cancellationToken);
            }
        }

        return contentItems;
    }
}
