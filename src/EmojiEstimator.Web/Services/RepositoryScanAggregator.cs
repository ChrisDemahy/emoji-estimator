namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanAggregator(IEmojiCounter emojiCounter, TimeProvider timeProvider) : IRepositoryScanAggregator
{
    public RepositoryScanResult Aggregate(
        string owner,
        string repository,
        IEnumerable<GitHubPullRequestBody> pullRequests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(pullRequests);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var pullRequestCount = 0;
        var pullRequestsWithEmojiCount = 0;
        var totalEmojiCount = 0;

        foreach (var pullRequest in pullRequests)
        {
            ArgumentNullException.ThrowIfNull(pullRequest);

            pullRequestCount++;

            var emojiCount = emojiCounter.CountEmojis(pullRequest.Body);
            totalEmojiCount += emojiCount;

            if (emojiCount > 0)
            {
                pullRequestsWithEmojiCount++;
            }
        }

        return new RepositoryScanResult
        {
            RepositoryOwner = trimmedOwner,
            RepositoryName = trimmedRepository,
            PullRequestCount = pullRequestCount,
            PullRequestsWithEmojiCount = pullRequestsWithEmojiCount,
            TotalEmojiCount = totalEmojiCount,
            AverageEmojisPerPullRequest = pullRequestCount == 0 ? 0m : totalEmojiCount / (decimal)pullRequestCount,
            ScannedAtUtc = timeProvider.GetUtcNow(),
        };
    }
}
