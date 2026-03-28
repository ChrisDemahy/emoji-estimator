using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class GitHubPullRequestReaderTests
{
    [Fact]
    public async Task ReadAllAsync_PaginatesAcrossMultiplePages()
    {
        var firstPage = Enumerable.Range(1, 100)
            .Select(number => new GitHubPullRequestBody(number, $"Body {number}"))
            .ToArray();
        var secondPage = new[]
        {
            new GitHubPullRequestBody(101, null),
            new GitHubPullRequestBody(102, string.Empty),
            new GitHubPullRequestBody(103, "🎉"),
        };
        var pageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubPullRequestBody>>
        {
            [1] = firstPage,
            [2] = secondPage,
        });
        var reader = new GitHubPullRequestReader(pageSource);

        var pullRequests = await reader.ReadAllAsync(" OctoCat ", " Hello-World ");

        Assert.Equal(103, pullRequests.Count);
        Assert.Equal([1, 2], pageSource.RequestedPages);
        Assert.Null(pullRequests[100].Body);
        Assert.Equal("🎉", pullRequests[102].Body);
    }

    [Fact]
    public async Task ReadAllAsync_ReportsProgressForEachFetchedPage()
    {
        var firstPage = Enumerable.Range(1, 100)
            .Select(number => new GitHubPullRequestBody(number, $"Body {number}"))
            .ToArray();
        var secondPage = new[]
        {
            new GitHubPullRequestBody(101, null),
            new GitHubPullRequestBody(102, "🎉"),
        };
        var pageSource = new FakeGitHubPullRequestPageSource(new Dictionary<int, IReadOnlyList<GitHubPullRequestBody>>
        {
            [1] = firstPage,
            [2] = secondPage,
        });
        var reader = new GitHubPullRequestReader(pageSource);
        var progressUpdates = new List<GitHubPullRequestReadProgress>();

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
                Assert.Equal(1, firstUpdate.PageNumber);
                Assert.Equal(100, firstUpdate.PagePullRequestCount);
                Assert.Equal(100, firstUpdate.PullRequestsRead);
            },
            secondUpdate =>
            {
                Assert.Equal(2, secondUpdate.PageNumber);
                Assert.Equal(2, secondUpdate.PagePullRequestCount);
                Assert.Equal(102, secondUpdate.PullRequestsRead);
            });
    }

    private sealed class FakeGitHubPullRequestPageSource(
        IReadOnlyDictionary<int, IReadOnlyList<GitHubPullRequestBody>> pages) : IGitHubPullRequestPageSource
    {
        public List<int> RequestedPages { get; } = [];

        public Task<IReadOnlyList<GitHubPullRequestBody>> ReadPageAsync(
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
                    : Array.Empty<GitHubPullRequestBody>());
        }
    }
}
