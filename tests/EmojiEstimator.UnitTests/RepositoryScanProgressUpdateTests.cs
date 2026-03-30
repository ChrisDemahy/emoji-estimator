using System.Text.Json;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanProgressUpdateTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateRunningPageProgress_ForPullRequestPage_IncludesPullRequestMessage()
    {
        var progress = new GitHubContentReadProgress(
            CurrentContentKind: GitHubContentKind.PullRequest,
            PageNumber: 3,
            PageItemCount: 25,
            PullRequestsRead: 75,
            IssuesRead: 0);
        var updatedAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.CreateRunningPageProgress(
            "dotnet", "aspnetcore", progress, updatedAt);

        Assert.Equal(RepositoryScanStatuses.Running, update.Status);
        Assert.Equal("DOTNET/ASPNETCORE", update.NormalizedKey);
        Assert.Equal("Fetched pull request page 3.", update.Message);
        Assert.Equal(3, update.CurrentPageNumber);
        Assert.Equal(GitHubContentKind.PullRequest, update.CurrentContentKind);
        Assert.Equal(25, update.CurrentPageItemCount);
        Assert.Equal(75, update.PullRequestsRead);
        Assert.Equal(0, update.IssuesRead);
        Assert.Equal(75, update.TotalItemsRead);
        Assert.Equal(updatedAt, update.UpdatedAtUtc);
    }

    [Fact]
    public void CreateRunningPageProgress_ForIssuePage_IncludesIssueMessage()
    {
        var progress = new GitHubContentReadProgress(
            CurrentContentKind: GitHubContentKind.Issue,
            PageNumber: 2,
            PageItemCount: 30,
            PullRequestsRead: 50,
            IssuesRead: 30);
        var updatedAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.CreateRunningPageProgress(
            "dotnet", "aspnetcore", progress, updatedAt);

        Assert.Equal("Fetched issue page 2.", update.Message);
        Assert.Equal(GitHubContentKind.Issue, update.CurrentContentKind);
        Assert.Equal(50, update.PullRequestsRead);
        Assert.Equal(30, update.IssuesRead);
        Assert.Equal(80, update.TotalItemsRead);
    }

    [Fact]
    public void CreateRunningPageProgress_ForRateLimitPause_IncludesRetryMessage()
    {
        var retryAtUtc = new DateTimeOffset(2026, 3, 28, 12, 5, 0, TimeSpan.Zero);
        var progress = GitHubContentReadProgress.CreateRateLimitBackoff(
            GitHubContentKind.PullRequest,
            pageNumber: 3,
            pullRequestsRead: 75,
            issuesRead: 0,
            retryAtUtc: retryAtUtc,
            retryDelay: TimeSpan.FromMinutes(5));
        var updatedAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.CreateRunningPageProgress(
            "dotnet", "aspnetcore", progress, updatedAt);

        Assert.Equal(
            "GitHub rate limit reached while fetching pull request page 3. Retrying at 2026-03-28 12:05:00 UTC.",
            update.Message);
        Assert.Null(update.CurrentPageItemCount);
        Assert.Equal(75, update.PullRequestsRead);
        Assert.Equal(75, update.TotalItemsRead);
    }

    [Fact]
    public void FromPersistedScan_Pending_ReturnsPendingStatusAndQueuedMessage()
    {
        RepositoryScan scan = CreateScan(RepositoryScanStatuses.Pending, updatedAt: new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc));

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal(RepositoryScanStatuses.Pending, update.Status);
        Assert.Equal("Scan queued.", update.Message);
        Assert.Equal("OCTOCAT/HELLO-WORLD", update.NormalizedKey);
    }

    [Fact]
    public void FromPersistedScan_Running_ReturnsRunningStatusWithGenericMessage()
    {
        RepositoryScan scan = CreateScan(RepositoryScanStatuses.Running, updatedAt: new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc));

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal(RepositoryScanStatuses.Running, update.Status);
        Assert.Equal("Scanning repository content...", update.Message);
        Assert.Equal("OCTOCAT/HELLO-WORLD", update.NormalizedKey);
    }

    [Fact]
    public void FromPersistedScan_Failed_ReturnsFailedStatusWithStoredMessage()
    {
        RepositoryScan scan = CreateScan(RepositoryScanStatuses.Failed, updatedAt: new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc));
        scan.FailureMessage = "Repository not found.";
        scan.CompletedAtUtc = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal(RepositoryScanStatuses.Failed, update.Status);
        Assert.Equal("Repository not found.", update.FailureMessage);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithBothPullRequestsAndIssues_ProducesCorrectMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 3, issueCount: 2);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal(RepositoryScanStatuses.Completed, update.Status);
        Assert.Equal("Scan completed after processing 3 pull requests and 2 issues.", update.Message);
        Assert.Equal(3, update.PullRequestsRead);
        Assert.Equal(2, update.IssuesRead);
        Assert.Equal(5, update.TotalItemsRead);
        Assert.NotNull(update.Result);
        Assert.Equal(3, update.Result.PullRequestSummary.ItemCount);
        Assert.Equal(2, update.Result.IssueSummary.ItemCount);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithSinglePullRequestOnly_ProducesSingularMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 1, issueCount: 0);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed after processing 1 pull request.", update.Message);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithMultiplePullRequestsOnly_ProducesPluralMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 4, issueCount: 0);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed after processing 4 pull requests.", update.Message);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithSingleIssueOnly_ProducesSingularMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 0, issueCount: 1);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed after processing 1 issue.", update.Message);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithMultipleIssuesOnly_ProducesPluralMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 0, issueCount: 5);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed after processing 5 issues.", update.Message);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithEmptyRepository_ProducesGenericMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 0, issueCount: 0);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed.", update.Message);
    }

    [Fact]
    public void FromPersistedScan_Completed_WithSinglePullRequestAndSingleIssue_ProducesSingularMessage()
    {
        RepositoryScan scan = CreateCompletedScan(pullRequestCount: 1, issueCount: 1);

        RepositoryScanProgressUpdate update = RepositoryScanProgressUpdate.FromPersistedScan(scan);

        Assert.Equal("Scan completed after processing 1 pull request and 1 issue.", update.Message);
    }

    private static RepositoryScan CreateScan(string status, DateTime updatedAt) =>
        new()
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = status,
            CreatedAtUtc = updatedAt,
            UpdatedAtUtc = updatedAt,
        };

    private static RepositoryScan CreateCompletedScan(int pullRequestCount, int issueCount)
    {
        var completedAt = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);
        var pullRequestSummary = RepositoryContentSummary.Create(pullRequestCount, 0, 0, 0, 0);
        var issueSummary = RepositoryContentSummary.Create(issueCount, 0, 0, 0, 0);
        var result = new RepositoryScanResult
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            PullRequestCount = pullRequestCount,
            PullRequestSummary = pullRequestSummary,
            IssueSummary = issueSummary,
            RepositorySummary = RepositoryContentSummary.Combine(pullRequestSummary, issueSummary),
            ScannedAtUtc = new DateTimeOffset(completedAt, TimeSpan.Zero),
        };

        return new RepositoryScan
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = RepositoryScan.CreateNormalizedKey("octocat", "hello-world"),
            Status = RepositoryScanStatuses.Completed,
            ResultJson = JsonSerializer.Serialize(result, SerializerOptions),
            CreatedAtUtc = completedAt.AddHours(-1),
            UpdatedAtUtc = completedAt,
            CompletedAtUtc = completedAt,
            ExpiresAtUtc = completedAt.AddHours(24),
        };
    }
}
