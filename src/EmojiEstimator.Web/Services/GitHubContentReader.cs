using Microsoft.Extensions.Options;

namespace EmojiEstimator.Web.Services;

public sealed class GitHubContentReader : IGitHubContentReader
{
    private const int PageSize = 100;
    private const int MaxPageNumber = int.MaxValue;
    private readonly IGitHubPullRequestPageSource pullRequestPageSource;
    private readonly IGitHubIssuePageSource issuePageSource;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan maxRateLimitWait;

    public GitHubContentReader(
        IGitHubPullRequestPageSource pullRequestPageSource,
        IGitHubIssuePageSource issuePageSource) : this(
            pullRequestPageSource,
            issuePageSource,
            Options.Create(new GitHubOptions()),
            TimeProvider.System)
    {
    }

    public GitHubContentReader(
        IGitHubPullRequestPageSource pullRequestPageSource,
        IGitHubIssuePageSource issuePageSource,
        IOptions<GitHubOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(pullRequestPageSource);
        ArgumentNullException.ThrowIfNull(issuePageSource);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var configuredMaxRateLimitWait = options.Value.MaxRateLimitWait;
        if (configuredMaxRateLimitWait <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("GitHub configuration is invalid. GitHub:MaxRateLimitWait must be greater than zero.");
        }

        this.pullRequestPageSource = pullRequestPageSource;
        this.issuePageSource = issuePageSource;
        this.timeProvider = timeProvider;
        maxRateLimitWait = configuredMaxRateLimitWait;
    }

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

            var contentPage = await ReadPullRequestPageWithBackoffAsync(
                trimmedOwner,
                trimmedRepository,
                pageNumber,
                pullRequestsRead,
                issuesRead,
                progressCallback,
                cancellationToken);

            if (contentPage.Count == 0)
            {
                break;
            }

            contentItems.AddRange(contentPage);
            pullRequestsRead += contentPage.Count;

            await ReportProgressAsync(
                new GitHubContentReadProgress(
                    GitHubContentKind.PullRequest,
                    pageNumber,
                    contentPage.Count,
                    pullRequestsRead,
                    issuesRead),
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

            var issuePage = await ReadIssuePageWithBackoffAsync(
                trimmedOwner,
                trimmedRepository,
                pageNumber,
                pullRequestsRead,
                issuesRead,
                progressCallback,
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
                new GitHubContentReadProgress(
                    GitHubContentKind.Issue,
                    pageNumber,
                    repositoryIssues.Length,
                    pullRequestsRead,
                    issuesRead),
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
        GitHubContentReadProgress progress,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback,
        CancellationToken cancellationToken)
    {
        if (progressCallback is null)
        {
            return ValueTask.CompletedTask;
        }

        return progressCallback(progress, cancellationToken);
    }

    private Task<IReadOnlyList<GitHubContentItem>> ReadPullRequestPageWithBackoffAsync(
        string owner,
        string repository,
        int pageNumber,
        int pullRequestsRead,
        int issuesRead,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback,
        CancellationToken cancellationToken) =>
        ReadPageWithBackoffAsync(
            GitHubContentKind.PullRequest,
            pageNumber,
            pullRequestsRead,
            issuesRead,
            progressCallback,
            readPageAsync: async innerCancellationToken =>
                await pullRequestPageSource.ReadPageAsync(
                    owner,
                    repository,
                    pageNumber,
                    PageSize,
                    innerCancellationToken),
            cancellationToken);

    private Task<IReadOnlyList<GitHubIssuePageItem>> ReadIssuePageWithBackoffAsync(
        string owner,
        string repository,
        int pageNumber,
        int pullRequestsRead,
        int issuesRead,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback,
        CancellationToken cancellationToken) =>
        ReadPageWithBackoffAsync(
            GitHubContentKind.Issue,
            pageNumber,
            pullRequestsRead,
            issuesRead,
            progressCallback,
            readPageAsync: async innerCancellationToken =>
                await issuePageSource.ReadPageAsync(
                    owner,
                    repository,
                    pageNumber,
                    PageSize,
                    innerCancellationToken),
            cancellationToken);

    private async Task<IReadOnlyList<TItem>> ReadPageWithBackoffAsync<TItem>(
        GitHubContentKind contentKind,
        int pageNumber,
        int pullRequestsRead,
        int issuesRead,
        Func<GitHubContentReadProgress, CancellationToken, ValueTask>? progressCallback,
        Func<CancellationToken, Task<IReadOnlyList<TItem>>> readPageAsync,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await readPageAsync(cancellationToken);
            }
            catch (GitHubRateLimitException exception) when (!cancellationToken.IsCancellationRequested)
            {
                var utcNow = timeProvider.GetUtcNow();
                var retryDelay = exception.GetRetryDelay(utcNow);
                if (retryDelay > maxRateLimitWait)
                {
                    throw new InvalidOperationException(
                        $"GitHub rate limit recovery for {DescribeContentKind(contentKind)} page {pageNumber} requires waiting {retryDelay:c}, which exceeds the configured maximum wait of {maxRateLimitWait:c}.",
                        exception);
                }

                var retryAtUtc = utcNow + retryDelay;
                await ReportProgressAsync(
                    GitHubContentReadProgress.CreateRateLimitBackoff(
                        contentKind,
                        pageNumber,
                        pullRequestsRead,
                        issuesRead,
                        retryAtUtc,
                        retryDelay),
                    progressCallback,
                    cancellationToken);

                if (retryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(retryDelay, timeProvider, cancellationToken);
                }
            }
        }
    }

    private static string DescribeContentKind(GitHubContentKind contentKind) =>
        contentKind == GitHubContentKind.Issue ? "issue" : "pull request";
}
