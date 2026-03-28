namespace EmojiEstimator.Web.Services;

public interface IGitHubPullRequestReader
{
    Task<IReadOnlyList<GitHubPullRequestBody>> ReadAllAsync(
        string owner,
        string repository,
        Func<GitHubPullRequestReadProgress, CancellationToken, ValueTask>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
