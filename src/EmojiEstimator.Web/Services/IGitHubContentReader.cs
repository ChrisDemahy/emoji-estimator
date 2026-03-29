namespace EmojiEstimator.Web.Services;

public interface IGitHubContentReader
{
    Task<IReadOnlyList<GitHubContentItem>> ReadAllAsync(
        string owner,
        string repository,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
