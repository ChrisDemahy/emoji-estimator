namespace EmojiEstimator.Web.Services;

public sealed record GitHubContentReadProgress(
    GitHubContentKind CurrentContentKind,
    int PageNumber,
    int PageItemCount,
    int PullRequestsRead,
    int IssuesRead)
{
    public int ItemsRead => PullRequestsRead + IssuesRead;
}
