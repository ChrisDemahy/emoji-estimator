using Microsoft.Extensions.Options;
using Octokit;

namespace EmojiEstimator.Web.Services;

public sealed class OctokitGitHubPullRequestPageSource : IGitHubPullRequestPageSource
{
    private static readonly ProductHeaderValue ProductHeader = new("emoji-estimator");
    private static readonly PullRequestRequest AllPullRequestsRequest = new()
    {
        State = ItemStateFilter.All,
    };

    private readonly GitHubClient client;

    public OctokitGitHubPullRequestPageSource(IOptions<GitHubOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        client = CreateClient(options.Value);
    }

    public async Task<IReadOnlyList<GitHubPullRequestBody>> ReadPageAsync(
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
            .Select(pullRequest => new GitHubPullRequestBody(pullRequest.Number, pullRequest.Body))
            .ToArray();
    }

    internal static GitHubClient CreateClient(GitHubOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var token = options.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"GitHub configuration is invalid. Set {GitHubOptions.SectionName}:Token to a personal access token.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                $"GitHub configuration is invalid. {GitHubOptions.SectionName}:BaseUrl must be an absolute URI.");
        }

        return new GitHubClient(ProductHeader, baseUri)
        {
            Credentials = new Credentials(token),
        };
    }
}
