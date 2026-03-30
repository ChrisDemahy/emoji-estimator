namespace EmojiEstimator.Web.Services;

public sealed record GitHubContentReadProgress(
    GitHubContentKind CurrentContentKind,
    int PageNumber,
    int PageItemCount,
    int PullRequestsRead,
    int IssuesRead,
    DateTimeOffset? RetryAtUtc = null,
    TimeSpan? RetryDelay = null)
{
    public int ItemsRead => PullRequestsRead + IssuesRead;

    public bool IsWaitingToRetry => RetryAtUtc is not null || RetryDelay is not null;

    public static GitHubContentReadProgress CreateRateLimitBackoff(
        GitHubContentKind currentContentKind,
        int pageNumber,
        int pullRequestsRead,
        int issuesRead,
        DateTimeOffset retryAtUtc,
        TimeSpan retryDelay) =>
        new(
            currentContentKind,
            pageNumber,
            0,
            pullRequestsRead,
            issuesRead,
            retryAtUtc,
            retryDelay);
}
