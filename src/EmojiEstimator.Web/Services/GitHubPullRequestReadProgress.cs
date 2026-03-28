namespace EmojiEstimator.Web.Services;

public sealed record GitHubPullRequestReadProgress(
    int PageNumber,
    int PagePullRequestCount,
    int PullRequestsRead);
