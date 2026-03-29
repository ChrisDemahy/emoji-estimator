namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanResult
{
    private RepositoryContentSummary? pullRequestSummary;
    private RepositoryContentSummary? issueSummary;
    private RepositoryContentSummary? repositorySummary;

    public string RepositoryOwner { get; init; } = string.Empty;

    public string RepositoryName { get; init; } = string.Empty;

    public int PullRequestCount { get; init; }

    public int PullRequestsWithEmojiCount { get; init; }

    public int TotalEmojiCount { get; init; }

    public decimal AverageEmojisPerPullRequest { get; init; }

    public RepositoryContentSummary PullRequestSummary
    {
        get => pullRequestSummary ??= CreateLegacyPullRequestSummary();
        init => pullRequestSummary = value;
    }

    public RepositoryContentSummary IssueSummary
    {
        get => issueSummary ??= RepositoryContentSummary.Empty;
        init => issueSummary = value;
    }

    public RepositoryContentSummary RepositorySummary
    {
        get => repositorySummary ??= CreateRepositorySummary();
        init => repositorySummary = value;
    }

    public DateTimeOffset ScannedAtUtc { get; init; }

    private RepositoryContentSummary CreateLegacyPullRequestSummary() =>
        new()
        {
            ItemCount = PullRequestCount,
            ItemsWithEmojiCount = PullRequestsWithEmojiCount,
            TotalEmojiCount = TotalEmojiCount,
            AverageEmojisPerItem = AverageEmojisPerPullRequest,
            ItemsWithEmDashCount = 0,
            TotalEmDashCount = 0,
            AverageEmDashesPerItem = 0m,
        };

    private RepositoryContentSummary CreateRepositorySummary()
    {
        var currentPullRequestSummary = PullRequestSummary;
        var currentIssueSummary = IssueSummary;

        if (currentPullRequestSummary.ItemCount == 0)
        {
            return currentIssueSummary;
        }

        if (currentIssueSummary.ItemCount == 0)
        {
            return currentPullRequestSummary;
        }

        return RepositoryContentSummary.Combine(currentPullRequestSummary, currentIssueSummary);
    }
}
