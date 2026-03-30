using Microsoft.Extensions.Options;
using Octokit;

namespace EmojiEstimator.Web.Services;

public sealed class OctokitGitHubIssuePageSource : IGitHubIssuePageSource
{
    private static readonly RepositoryIssueRequest AllIssuesRequest = new()
    {
        State = ItemStateFilter.All,
    };

    private readonly GitHubClient client;

    public OctokitGitHubIssuePageSource(IOptions<GitHubOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        client = OctokitGitHubClientFactory.CreateClient(options.Value);
    }

    public async Task<IReadOnlyList<GitHubIssuePageItem>> ReadPageAsync(
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

        IReadOnlyList<Issue> issues;

        try
        {
            issues = await client.Issue.GetAllForRepository(
                owner.Trim(),
                repository.Trim(),
                AllIssuesRequest,
                new ApiOptions
                {
                    StartPage = pageNumber,
                    PageCount = 1,
                    PageSize = pageSize,
                });
        }
        catch (Exception exception) when (GitHubRateLimitExceptionTranslator.TryCreate(exception, out var rateLimitException))
        {
            throw rateLimitException!;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return issues
            .Select(issue => new GitHubIssuePageItem(issue.Number, issue.Body, issue.PullRequest is not null))
            .ToArray();
    }
}
