using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanAggregatorTests
{
    [Fact]
    public void Aggregate_ReturnsZeroStatisticsForRepositoryWithoutGitHubContent()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var aggregator = new RepositoryScanAggregator(
            new UnicodeEmojiCounter(),
            new CanonicalEmDashCounter(),
            new FixedTimeProvider(utcNow));

        var result = aggregator.Aggregate(" OctoCat ", " Hello-World ", Array.Empty<GitHubContentItem>());

        Assert.Equal("OctoCat", result.RepositoryOwner);
        Assert.Equal("Hello-World", result.RepositoryName);
        Assert.Equal(0, result.PullRequestCount);
        Assert.Equal(0, result.PullRequestsWithEmojiCount);
        Assert.Equal(0, result.TotalEmojiCount);
        Assert.Equal(0m, result.AverageEmojisPerPullRequest);
        Assert.Equal(0, result.PullRequestSummary.ItemCount);
        Assert.Equal(0, result.IssueSummary.ItemCount);
        Assert.Equal(0, result.RepositorySummary.ItemCount);
        Assert.Equal(0, result.RepositorySummary.TotalEmDashCount);
        Assert.Equal(utcNow, result.ScannedAtUtc);
    }

    [Fact]
    public void Aggregate_ComputesSeparatePullRequestIssueAndCombinedSummaries()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var aggregator = new RepositoryScanAggregator(
            new UnicodeEmojiCounter(),
            new CanonicalEmDashCounter(),
            new FixedTimeProvider(utcNow));

        var result = aggregator.Aggregate(
            "octocat",
            "hello-world",
            [
                GitHubContentItem.CreatePullRequest(1, "🎉🎉 —"),
                GitHubContentItem.CreatePullRequest(2, "Ship it 👍🏽 —"),
                GitHubContentItem.CreateIssue(3, "Needs follow-up —"),
                GitHubContentItem.CreateIssue(4, "Closed 🎉"),
            ]);

        Assert.Equal(2, result.PullRequestCount);
        Assert.Equal(2, result.PullRequestsWithEmojiCount);
        Assert.Equal(3, result.TotalEmojiCount);
        Assert.Equal(1.5m, result.AverageEmojisPerPullRequest);

        Assert.Equal(2, result.PullRequestSummary.ItemCount);
        Assert.Equal(2, result.PullRequestSummary.ItemsWithEmojiCount);
        Assert.Equal(3, result.PullRequestSummary.TotalEmojiCount);
        Assert.Equal(1.5m, result.PullRequestSummary.AverageEmojisPerItem);
        Assert.Equal(2, result.PullRequestSummary.ItemsWithEmDashCount);
        Assert.Equal(2, result.PullRequestSummary.TotalEmDashCount);
        Assert.Equal(1m, result.PullRequestSummary.AverageEmDashesPerItem);

        Assert.Equal(2, result.IssueSummary.ItemCount);
        Assert.Equal(1, result.IssueSummary.ItemsWithEmojiCount);
        Assert.Equal(1, result.IssueSummary.TotalEmojiCount);
        Assert.Equal(0.5m, result.IssueSummary.AverageEmojisPerItem);
        Assert.Equal(1, result.IssueSummary.ItemsWithEmDashCount);
        Assert.Equal(1, result.IssueSummary.TotalEmDashCount);
        Assert.Equal(0.5m, result.IssueSummary.AverageEmDashesPerItem);

        Assert.Equal(4, result.RepositorySummary.ItemCount);
        Assert.Equal(3, result.RepositorySummary.ItemsWithEmojiCount);
        Assert.Equal(4, result.RepositorySummary.TotalEmojiCount);
        Assert.Equal(1m, result.RepositorySummary.AverageEmojisPerItem);
        Assert.Equal(3, result.RepositorySummary.ItemsWithEmDashCount);
        Assert.Equal(3, result.RepositorySummary.TotalEmDashCount);
        Assert.Equal(0.75m, result.RepositorySummary.AverageEmDashesPerItem);
    }

    [Fact]
    public void Aggregate_KeepsLegacyFlatFieldsBoundToPullRequestMetrics()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var aggregator = new RepositoryScanAggregator(
            new UnicodeEmojiCounter(),
            new CanonicalEmDashCounter(),
            new FixedTimeProvider(utcNow));

        var result = aggregator.Aggregate(
            "octocat",
            "hello-world",
            [
                GitHubContentItem.CreatePullRequest(1, "Ship it 🚀"),
                GitHubContentItem.CreateIssue(2, "🎉🎉"),
            ]);

        Assert.Equal(1, result.PullRequestCount);
        Assert.Equal(1, result.PullRequestsWithEmojiCount);
        Assert.Equal(1, result.TotalEmojiCount);
        Assert.Equal(1m, result.AverageEmojisPerPullRequest);
        Assert.Equal(3, result.RepositorySummary.TotalEmojiCount);
    }
}
