using System.Text.Json;
using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanResultCompatibilityTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void JsonSerialization_DeserializesLegacyCachedRowsAndSerializesAdditiveSummaries()
    {
        var legacyJson = JsonSerializer.Serialize(
            new
            {
                repositoryOwner = "octocat",
                repositoryName = "hello-world",
                pullRequestCount = 2,
                pullRequestsWithEmojiCount = 1,
                totalEmojiCount = 3,
                averageEmojisPerPullRequest = 1.5m,
                scannedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
            },
            SerializerOptions);

        var result = JsonSerializer.Deserialize<RepositoryScanResult>(legacyJson, SerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.PullRequestSummary.ItemCount);
        Assert.Equal(1, result.PullRequestSummary.ItemsWithEmojiCount);
        Assert.Equal(3, result.PullRequestSummary.TotalEmojiCount);
        Assert.Equal(1.5m, result.PullRequestSummary.AverageEmojisPerItem);
        Assert.Equal(0, result.PullRequestSummary.TotalEmDashCount);
        Assert.Equal(0, result.IssueSummary.ItemCount);
        Assert.Equal(2, result.RepositorySummary.ItemCount);
        Assert.Equal(3, result.RepositorySummary.TotalEmojiCount);

        var additiveJson = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("\"pullRequestCount\":2", additiveJson, StringComparison.Ordinal);
        Assert.Contains("\"pullRequestSummary\":{\"itemCount\":2", additiveJson, StringComparison.Ordinal);
        Assert.Contains("\"issueSummary\":{\"itemCount\":0", additiveJson, StringComparison.Ordinal);
        Assert.Contains("\"repositorySummary\":{\"itemCount\":2", additiveJson, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonSerialization_RoundTripsExpandedSummariesWithEmDashMetrics()
    {
        var originalResult = new RepositoryScanResult
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            PullRequestCount = 2,
            PullRequestsWithEmojiCount = 1,
            TotalEmojiCount = 3,
            AverageEmojisPerPullRequest = 1.5m,
            PullRequestSummary = RepositoryContentSummary.Create(2, 1, 3, 2, 4),
            IssueSummary = RepositoryContentSummary.Create(3, 2, 5, 1, 1),
            RepositorySummary = RepositoryContentSummary.Create(5, 3, 8, 3, 5),
            ScannedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(originalResult, SerializerOptions);
        var roundTrippedResult = JsonSerializer.Deserialize<RepositoryScanResult>(json, SerializerOptions);

        Assert.NotNull(roundTrippedResult);
        Assert.Equal(2, roundTrippedResult.PullRequestSummary.ItemCount);
        Assert.Equal(4, roundTrippedResult.PullRequestSummary.TotalEmDashCount);
        Assert.Equal(3, roundTrippedResult.IssueSummary.ItemCount);
        Assert.Equal(1, roundTrippedResult.IssueSummary.TotalEmDashCount);
        Assert.Equal(5, roundTrippedResult.RepositorySummary.ItemCount);
        Assert.Equal(5, roundTrippedResult.RepositorySummary.TotalEmDashCount);
        Assert.Equal(1m, roundTrippedResult.RepositorySummary.AverageEmDashesPerItem);
        Assert.Contains("\"averageEmojisPerPullRequest\":1.5", json, StringComparison.Ordinal);
        Assert.Contains("\"pullRequestSummary\":{\"itemCount\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"repositorySummary\":{\"itemCount\":5", json, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonSerialization_DerivesRepositorySummaryWhenCachedRowsOmitCombinedSummary()
    {
        var additiveButIncompleteJson = JsonSerializer.Serialize(
            new
            {
                repositoryOwner = "octocat",
                repositoryName = "hello-world",
                pullRequestCount = 2,
                pullRequestsWithEmojiCount = 1,
                totalEmojiCount = 3,
                averageEmojisPerPullRequest = 1.5m,
                pullRequestSummary = new
                {
                    itemCount = 2,
                    itemsWithEmojiCount = 1,
                    totalEmojiCount = 3,
                    averageEmojisPerItem = 1.5m,
                    itemsWithEmDashCount = 2,
                    totalEmDashCount = 4,
                    averageEmDashesPerItem = 2m
                },
                issueSummary = new
                {
                    itemCount = 3,
                    itemsWithEmojiCount = 2,
                    totalEmojiCount = 5,
                    averageEmojisPerItem = 1.67m,
                    itemsWithEmDashCount = 1,
                    totalEmDashCount = 1,
                    averageEmDashesPerItem = 0.33m
                },
                scannedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero)
            },
            SerializerOptions);

        var result = JsonSerializer.Deserialize<RepositoryScanResult>(additiveButIncompleteJson, SerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(5, result.RepositorySummary.ItemCount);
        Assert.Equal(3, result.RepositorySummary.ItemsWithEmojiCount);
        Assert.Equal(8, result.RepositorySummary.TotalEmojiCount);
        Assert.Equal(3, result.RepositorySummary.ItemsWithEmDashCount);
        Assert.Equal(5, result.RepositorySummary.TotalEmDashCount);
    }
}
