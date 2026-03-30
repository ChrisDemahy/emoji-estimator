using EmojiEstimator.Web.Services;
using Microsoft.Extensions.Options;

namespace EmojiEstimator.UnitTests;

public sealed class GitHubContentReaderTests
{
    [Fact]
    public async Task ReadAllAsync_PaginatesAcrossPullRequestAndIssuePages()
    {
        var firstPullRequestPage = Enumerable.Range(1, 100)
            .Select(number => GitHubContentItem.CreatePullRequest(number, $"Body {number}"))
            .ToArray();
        var secondPullRequestPage = new[]
        {
            GitHubContentItem.CreatePullRequest(101, null),
            GitHubContentItem.CreatePullRequest(102, string.Empty),
            GitHubContentItem.CreatePullRequest(103, "🎉"),
        };
        var firstIssuePage = Enumerable.Range(201, 100)
            .Select(number => number % 10 == 0
                ? GitHubIssuePageItem.CreatePullRequest(number, $"Pull request {number}")
                : GitHubIssuePageItem.CreateIssue(number, $"Issue {number}"))
            .ToArray();
        var secondIssuePage = new[]
        {
            GitHubIssuePageItem.CreateIssue(301, null),
            GitHubIssuePageItem.CreateIssue(302, "🐙"),
        };
        var pullRequestPageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubContentItem>>
        {
            [1] = firstPullRequestPage,
            [2] = secondPullRequestPage,
        });
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>
        {
            [1] = firstIssuePage,
            [2] = secondIssuePage,
        });
        var reader = new GitHubContentReader(pullRequestPageSource, issuePageSource);

        var contentItems = await reader.ReadAllAsync(" OctoCat ", " Hello-World ");

        Assert.Equal(195, contentItems.Count);
        Assert.Equal([1, 2], pullRequestPageSource.RequestedPages);
        Assert.Equal([1, 2], issuePageSource.RequestedPages);
        Assert.Null(contentItems[100].Body);
        Assert.Equal(GitHubContentKind.PullRequest, contentItems[100].Kind);
        Assert.Equal("🎉", contentItems[102].Body);
        Assert.Equal(GitHubContentKind.Issue, contentItems[103].Kind);
        Assert.DoesNotContain(contentItems, contentItem => contentItem is { Kind: GitHubContentKind.Issue, Number: 210 });
        Assert.Equal("🐙", contentItems[^1].Body);
    }

    [Fact]
    public async Task ReadAllAsync_ReportsProgressForEachFetchedPullRequestAndIssuePage()
    {
        var pullRequestPage = new[]
        {
            GitHubContentItem.CreatePullRequest(101, null),
            GitHubContentItem.CreatePullRequest(102, "🎉"),
        };
        var issuePage = new[]
        {
            GitHubIssuePageItem.CreateIssue(201, null),
            GitHubIssuePageItem.CreatePullRequest(202, "Duplicate pull request"),
        };
        var pullRequestPageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubContentItem>>
        {
            [1] = pullRequestPage,
        });
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>
        {
            [1] = issuePage,
        });
        var reader = new GitHubContentReader(pullRequestPageSource, issuePageSource);
        var progressUpdates = new List<GitHubContentReadProgress>();

        await reader.ReadAllAsync(
            "octocat",
            "hello-world",
            (progress, cancellationToken) =>
            {
                progressUpdates.Add(progress);
                return ValueTask.CompletedTask;
            });

        Assert.Collection(
            progressUpdates,
            firstUpdate =>
            {
                Assert.Equal(GitHubContentKind.PullRequest, firstUpdate.CurrentContentKind);
                Assert.Equal(1, firstUpdate.PageNumber);
                Assert.Equal(2, firstUpdate.PageItemCount);
                Assert.Equal(2, firstUpdate.PullRequestsRead);
                Assert.Equal(0, firstUpdate.IssuesRead);
                Assert.Equal(2, firstUpdate.ItemsRead);
            },
            secondUpdate =>
            {
                Assert.Equal(GitHubContentKind.Issue, secondUpdate.CurrentContentKind);
                Assert.Equal(1, secondUpdate.PageNumber);
                Assert.Equal(1, secondUpdate.PageItemCount);
                Assert.Equal(2, secondUpdate.PullRequestsRead);
                Assert.Equal(1, secondUpdate.IssuesRead);
                Assert.Equal(3, secondUpdate.ItemsRead);
             });
    }

    [Fact]
    public async Task ReadAllAsync_SkipsIssuePagesThatOnlyContainPullRequests()
    {
        var pullRequestPageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubContentItem>>
        {
            [1] =
            [
                GitHubContentItem.CreatePullRequest(101, "🎉")
            ]
        });
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>
        {
            [1] =
            [
                GitHubIssuePageItem.CreatePullRequest(201, "Already counted"),
                GitHubIssuePageItem.CreatePullRequest(202, "Still a pull request"),
            ]
        });
        var reader = new GitHubContentReader(pullRequestPageSource, issuePageSource);
        var progressUpdates = new List<GitHubContentReadProgress>();

        var contentItems = await reader.ReadAllAsync(
            "octocat",
            "hello-world",
            (progress, cancellationToken) =>
            {
                progressUpdates.Add(progress);
                return ValueTask.CompletedTask;
            });

        Assert.Collection(
            contentItems,
            contentItem =>
            {
                Assert.Equal(GitHubContentKind.PullRequest, contentItem.Kind);
                Assert.Equal(101, contentItem.Number);
            });
        Assert.Collection(
            progressUpdates,
            firstUpdate =>
            {
                Assert.Equal(GitHubContentKind.PullRequest, firstUpdate.CurrentContentKind);
                Assert.Equal(1, firstUpdate.PageItemCount);
                Assert.Equal(1, firstUpdate.PullRequestsRead);
                Assert.Equal(0, firstUpdate.IssuesRead);
                Assert.Equal(1, firstUpdate.ItemsRead);
            },
            secondUpdate =>
            {
                Assert.Equal(GitHubContentKind.Issue, secondUpdate.CurrentContentKind);
                Assert.Equal(0, secondUpdate.PageItemCount);
                Assert.Equal(1, secondUpdate.PullRequestsRead);
                Assert.Equal(0, secondUpdate.IssuesRead);
                Assert.Equal(1, secondUpdate.ItemsRead);
            });
    }

    [Fact]
    public async Task ReadAllAsync_PreservesIssueOrderAfterFilteringPullRequestBackedItems()
    {
        var pullRequestPageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubContentItem>>
        {
            [1] =
            [
                GitHubContentItem.CreatePullRequest(100, "First pull request")
            ]
        });
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>
        {
            [1] =
            [
                GitHubIssuePageItem.CreateIssue(201, "First issue"),
                GitHubIssuePageItem.CreatePullRequest(202, "Hidden pull request"),
                GitHubIssuePageItem.CreateIssue(203, "Second issue"),
                GitHubIssuePageItem.CreatePullRequest(204, "Another hidden pull request"),
                GitHubIssuePageItem.CreateIssue(205, "Third issue"),
            ]
        });
        var reader = new GitHubContentReader(pullRequestPageSource, issuePageSource);

        var contentItems = await reader.ReadAllAsync("octocat", "hello-world");

        Assert.Equal(
            [100, 201, 203, 205],
            contentItems.Select(contentItem => contentItem.Number).ToArray());
        Assert.Equal(
            [GitHubContentKind.PullRequest, GitHubContentKind.Issue, GitHubContentKind.Issue, GitHubContentKind.Issue],
            contentItems.Select(contentItem => contentItem.Kind).ToArray());
    }

    [Fact]
    public async Task ReadAllAsync_WaitsForRateLimitAndRetriesTheSamePullRequestPage()
    {
        var utcNow = new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero);
        var pullRequestPageSource = new SequencedGitHubPullRequestPageSource(
        [
            GitHubRateLimitException.CreateWithRetryAfter(TimeSpan.Zero, "GitHub asked for a retry."),
            new[]
            {
                GitHubContentItem.CreatePullRequest(101, "🎉")
            }
        ]);
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>());
        var reader = new GitHubContentReader(
            pullRequestPageSource,
            issuePageSource,
            Options.Create(new GitHubOptions
            {
                MaxRateLimitWait = TimeSpan.FromMinutes(1),
            }),
            new FixedTimeProvider(utcNow));
        var progressUpdates = new List<GitHubContentReadProgress>();

        var contentItems = await reader.ReadAllAsync(
            "octocat",
            "hello-world",
            (progress, cancellationToken) =>
            {
                progressUpdates.Add(progress);
                return ValueTask.CompletedTask;
            });

        Assert.Collection(
            contentItems,
            contentItem =>
            {
                Assert.Equal(GitHubContentKind.PullRequest, contentItem.Kind);
                Assert.Equal(101, contentItem.Number);
            });
        Assert.Equal([1, 1], pullRequestPageSource.RequestedPages);
        Assert.Collection(
            progressUpdates,
            retryUpdate =>
            {
                Assert.True(retryUpdate.IsWaitingToRetry);
                Assert.Equal(GitHubContentKind.PullRequest, retryUpdate.CurrentContentKind);
                Assert.Equal(1, retryUpdate.PageNumber);
                Assert.Equal(0, retryUpdate.PageItemCount);
                Assert.Equal(utcNow, retryUpdate.RetryAtUtc);
            },
            fetchedUpdate =>
            {
                Assert.False(fetchedUpdate.IsWaitingToRetry);
                Assert.Equal(GitHubContentKind.PullRequest, fetchedUpdate.CurrentContentKind);
                Assert.Equal(1, fetchedUpdate.PageNumber);
                Assert.Equal(1, fetchedUpdate.PageItemCount);
                Assert.Equal(1, fetchedUpdate.PullRequestsRead);
                Assert.Equal(0, fetchedUpdate.IssuesRead);
            });
    }

    [Fact]
    public async Task ReadAllAsync_ThrowsWhenRateLimitDelayExceedsConfiguredMaxWait()
    {
        var pullRequestPageSource = new SequencedGitHubPullRequestPageSource(
        [
            GitHubRateLimitException.CreateWithRetryAfter(TimeSpan.FromMinutes(10), "GitHub asked for a retry.")
        ]);
        var issuePageSource = new FakeGitHubIssuePageSource(new Dictionary<int, IReadOnlyList<GitHubIssuePageItem>>());
        var reader = new GitHubContentReader(
            pullRequestPageSource,
            issuePageSource,
            Options.Create(new GitHubOptions
            {
                MaxRateLimitWait = TimeSpan.FromSeconds(5),
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.ReadAllAsync("octocat", "hello-world"));

        Assert.Contains("configured maximum wait", exception.Message);
        Assert.Equal([1], pullRequestPageSource.RequestedPages);
    }

    private sealed class FakeGitHubPullRequestPageSource(
        IReadOnlyDictionary<int, IReadOnlyList<GitHubContentItem>> pages) : IGitHubPullRequestPageSource
    {
        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<GitHubContentItem>> ReadPageAsync(
            string owner,
            string repository,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(pageNumber);

            return Task.FromResult(
                pages.TryGetValue(pageNumber, out var page)
                    ? page
                    : Array.Empty<GitHubContentItem>());
        }
    }

    private sealed class FakeGitHubIssuePageSource(
        IReadOnlyDictionary<int, IReadOnlyList<GitHubIssuePageItem>> pages) : IGitHubIssuePageSource
    {
        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<GitHubIssuePageItem>> ReadPageAsync(
            string owner,
            string repository,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(pageNumber);

            return Task.FromResult(
                pages.TryGetValue(pageNumber, out var page)
                    ? page
                    : Array.Empty<GitHubIssuePageItem>());
        }
    }

    private sealed class SequencedGitHubPullRequestPageSource(IEnumerable<object> responses) : IGitHubPullRequestPageSource
    {
        private readonly Queue<object> responses = new(responses);

        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<GitHubContentItem>> ReadPageAsync(
            string owner,
            string repository,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(pageNumber);

            if (responses.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<GitHubContentItem>>(Array.Empty<GitHubContentItem>());
            }

            var response = responses.Dequeue();
            if (response is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((IReadOnlyList<GitHubContentItem>)response);
        }
    }
}
