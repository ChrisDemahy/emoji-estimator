namespace EmojiEstimator.Web.Services;

public sealed class GitHubPullRequestReader(IGitHubPullRequestPageSource pageSource) : IGitHubPullRequestReader
{
    private const int PageSize = 100;
    private const int MaxPageNumber = int.MaxValue;

    public async Task<IReadOnlyList<GitHubPullRequestBody>> ReadAllAsync(
        string owner,
        string repository,
        Func<GitHubPullRequestReadProgress, CancellationToken, ValueTask>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var pullRequests = new List<GitHubPullRequestBody>();

        for (var pageNumber = 1; ; pageNumber++)
        {
            if (pageNumber == MaxPageNumber)
            {
                throw new InvalidOperationException("GitHub pull request pagination exceeded the supported page count.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var page = await pageSource.ReadPageAsync(
                trimmedOwner,
                trimmedRepository,
                pageNumber,
                PageSize,
                cancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            pullRequests.AddRange(page);

            if (progressCallback is not null)
            {
                await progressCallback(
                    new GitHubPullRequestReadProgress(
                        pageNumber,
                        page.Count,
                        pullRequests.Count),
                    cancellationToken);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        return pullRequests;
    }
}
