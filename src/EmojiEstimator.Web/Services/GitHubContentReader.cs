namespace EmojiEstimator.Web.Services;

public sealed class GitHubContentReader(
    IGitHubPullRequestPageSource pullRequestPageSource,
    IGitHubIssuePageSource issuePageSource) : IGitHubContentReader
{
    private const int PageSize = 100;
    private const int MaxPageNumber = int.MaxValue;

    public async Task<IReadOnlyList<GitHubContentItem>> ReadAllAsync(
        string owner,
        string repository,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var contentItems = new List<GitHubContentItem>();
        var pullRequestsRead = 0;
        var issuesRead = 0;

        for (var pageNumber = 1; ; pageNumber++)
        {
            if (pageNumber == MaxPageNumber)
            {
                throw new InvalidOperationException("GitHub content pagination exceeded the supported page count.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var contentPage = await pullRequestPageSource.ReadPageAsync(
                trimmedOwner,
                trimmedRepository,
                pageNumber,
                PageSize,
                cancellationToken);

            if (contentPage.Count == 0)
            {
                break;
            }

            contentItems.AddRange(contentPage);
            pullRequestsRead += contentPage.Count;

            await ReportProgressAsync(
                GitHubContentKind.PullRequest,
                pageNumber,
                contentPage.Count,
                pullRequestsRead,
                issuesRead,
                progressCallback,
                cancellationToken);

            if (contentPage.Count < PageSize)
            {
                break;
            }
        }

        for (var pageNumber = 1; ; pageNumber++)
        {
            if (pageNumber == MaxPageNumber)
            {
                throw new InvalidOperationException("GitHub content pagination exceeded the supported page count.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var issuePage = await issuePageSource.ReadPageAsync(
                trimmedOwner,
                trimmedRepository,
                pageNumber,
                PageSize,
                cancellationToken);

            if (issuePage.Count == 0)
            {
                break;
            }

            var repositoryIssues = issuePage
                .Where(issue => !issue.IsPullRequest)
                .Select(issue => GitHubContentItem.CreateIssue(issue.Number, issue.Body))
                .ToArray();

            contentItems.AddRange(repositoryIssues);
            issuesRead += repositoryIssues.Length;

            await ReportProgressAsync(
                GitHubContentKind.Issue,
                pageNumber,
                repositoryIssues.Length,
                pullRequestsRead,
                issuesRead,
                progressCallback,
                cancellationToken);

            if (issuePage.Count < PageSize)
            {
                break;
            }
        }

        return contentItems;
    }

    private static ValueTask ReportProgressAsync(
        GitHubContentKind currentContentKind,
        int pageNumber,
        int pageItemCount,
        int pullRequestsRead,
        int issuesRead,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback,
        CancellationToken cancellationToken)
    {
        if (progressCallback is null)
        {
            return ValueTask.CompletedTask;
        }

        return progressCallback(
            new GitHubContentReadProgress(
                currentContentKind,
                pageNumber,
                pageItemCount,
                pullRequestsRead,
                issuesRead),
            cancellationToken);
    }
}
