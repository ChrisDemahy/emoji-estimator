namespace EmojiEstimator.Web.Services;

public interface IGitHubIssuePageSource
{
    Task<IReadOnlyList<GitHubIssuePageItem>> ReadPageAsync(
        string owner,
        string repository,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
