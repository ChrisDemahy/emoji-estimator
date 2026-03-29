using Microsoft.Extensions.Options;
using Octokit;

namespace EmojiEstimator.Web.Services;

public sealed class OctokitGitHubPullRequestPageSource : IGitHubPullRequestPageSource
{
    private static readonly PullRequestRequest AllPullRequestsRequest = new()
    {
        State = ItemStateFilter.All,
    };

    private readonly GitHubClient client;

    public OctokitGitHubPullRequestPageSource(IOptions<GitHubOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        client = OctokitGitHubClientFactory.CreateClient(options.Value);
    }

    public async Task<IReadOnlyList<GitHubContentItem>> ReadPageAsync(
        string owner,
        string repository,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        cancellationToken.ThrowIfCancellationRequested();

        var pullRequests = await client.PullRequest.GetAllForRepository(
            owner.Trim(),
            repository.Trim(),
            AllPullRequestsRequest,
            new ApiOptions
            {
                StartPage = pageNumber,
                PageCount = 1,
                PageSize = pageSize,
            });

        cancellationToken.ThrowIfCancellationRequested();

        return pullRequests
            .Select(pullRequest => GitHubContentItem.CreatePullRequest(pullRequest.Number, pullRequest.Body))
            .ToArray();
    }
}
