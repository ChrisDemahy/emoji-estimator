using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanAggregatorTests
{
    [Fact]
    public void Aggregate_ReturnsZeroStatisticsForRepositoryWithoutPullRequests()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var aggregator = new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow));

        var result = aggregator.Aggregate(" OctoCat ", " Hello-World ", Array.Empty<GitHubPullRequestBody>());

        Assert.Equal("OctoCat", result.RepositoryOwner);
        Assert.Equal("Hello-World", result.RepositoryName);
        Assert.Equal(0, result.PullRequestCount);
        Assert.Equal(0, result.PullRequestsWithEmojiCount);
        Assert.Equal(0, result.TotalEmojiCount);
        Assert.Equal(0m, result.AverageEmojisPerPullRequest);
        Assert.Equal(utcNow, result.ScannedAtUtc);
    }

    [Fact]
    public void Aggregate_ComputesEmojiTotalsAcrossPullRequestBodies()
    {
        var utcNow = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero);
        var aggregator = new RepositoryScanAggregator(new UnicodeEmojiCounter(), new FixedTimeProvider(utcNow));

        var result = aggregator.Aggregate(
            "octocat",
            "hello-world",
            [
                new GitHubPullRequestBody(1, "🎉🎉"),
                new GitHubPullRequestBody(2, null),
                new GitHubPullRequestBody(3, "Ship it 👍🏽"),
            ]);

        Assert.Equal(3, result.PullRequestCount);
        Assert.Equal(2, result.PullRequestsWithEmojiCount);
        Assert.Equal(3, result.TotalEmojiCount);
        Assert.Equal(1m, result.AverageEmojisPerPullRequest);
    }
}
