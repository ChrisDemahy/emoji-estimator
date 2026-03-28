namespace EmojiEstimator.Web.Services;

public interface IGitHubPullRequestPageSource
{
    Task<IReadOnlyList<GitHubPullRequestBody>> ReadPageAsync(
        string owner,
        string repository,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
