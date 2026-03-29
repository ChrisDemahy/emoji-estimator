namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanAggregator(
    IEmojiCounter emojiCounter,
    IEmDashCounter emDashCounter,
    TimeProvider timeProvider) : IRepositoryScanAggregator
{
    public RepositoryScanResult Aggregate(
        string owner,
        string repository,
        IEnumerable<GitHubContentItem> contentItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(contentItems);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var pullRequestTotals = new ContentTotals();
        var issueTotals = new ContentTotals();

        foreach (var contentItem in contentItems)
        {
            ArgumentNullException.ThrowIfNull(contentItem);

            var contentTotals = contentItem.Kind == GitHubContentKind.Issue
                ? issueTotals
                : pullRequestTotals;

            contentTotals.ItemCount++;

            var emojiCount = emojiCounter.CountEmojis(contentItem.Body);
            contentTotals.TotalEmojiCount += emojiCount;

            if (emojiCount > 0)
            {
                contentTotals.ItemsWithEmojiCount++;
            }

            var emDashCount = emDashCounter.CountEmDashes(contentItem.Body);
            contentTotals.TotalEmDashCount += emDashCount;

            if (emDashCount > 0)
            {
                contentTotals.ItemsWithEmDashCount++;
            }
        }

        var pullRequestSummary = pullRequestTotals.ToSummary();
        var issueSummary = issueTotals.ToSummary();

        return new RepositoryScanResult
        {
            RepositoryOwner = trimmedOwner,
            RepositoryName = trimmedRepository,
            PullRequestCount = pullRequestSummary.ItemCount,
            PullRequestsWithEmojiCount = pullRequestSummary.ItemsWithEmojiCount,
            TotalEmojiCount = pullRequestSummary.TotalEmojiCount,
            AverageEmojisPerPullRequest = pullRequestSummary.AverageEmojisPerItem,
            PullRequestSummary = pullRequestSummary,
            IssueSummary = issueSummary,
            RepositorySummary = RepositoryContentSummary.Combine(pullRequestSummary, issueSummary),
            ScannedAtUtc = timeProvider.GetUtcNow(),
        };
    }

    private sealed class ContentTotals
    {
        public int ItemCount { get; set; }

        public int ItemsWithEmojiCount { get; set; }

        public int TotalEmojiCount { get; set; }

        public int ItemsWithEmDashCount { get; set; }

        public int TotalEmDashCount { get; set; }

        public RepositoryContentSummary ToSummary() =>
            RepositoryContentSummary.Create(
                ItemCount,
                ItemsWithEmojiCount,
                TotalEmojiCount,
                ItemsWithEmDashCount,
                TotalEmDashCount);
    }
}
