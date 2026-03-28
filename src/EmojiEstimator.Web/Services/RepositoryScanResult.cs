namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanResult
{
    public string RepositoryOwner { get; init; } = string.Empty;

    public string RepositoryName { get; init; } = string.Empty;

    public int PullRequestCount { get; init; }

    public int PullRequestsWithEmojiCount { get; init; }

    public int TotalEmojiCount { get; init; }

    public decimal AverageEmojisPerPullRequest { get; init; }

    public DateTimeOffset ScannedAtUtc { get; init; }
}
