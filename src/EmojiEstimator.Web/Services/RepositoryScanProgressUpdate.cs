using EmojiEstimator.Web.Data;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanProgressUpdate
{
    public string RepositoryOwner { get; init; } = string.Empty;

    public string RepositoryName { get; init; } = string.Empty;

    public string NormalizedKey { get; init; } = string.Empty;

    public string Status { get; init; } = RepositoryScanStatuses.Pending;

    public string Message { get; init; } = string.Empty;

    public int? CurrentPageNumber { get; init; }

    public GitHubContentKind? CurrentContentKind { get; init; }

    public int? CurrentPageItemCount { get; init; }

    public int? PullRequestsRead { get; init; }

    public int? IssuesRead { get; init; }

    public int? TotalItemsRead { get; init; }

    public int? PercentComplete { get; init; }

    public string? FailureMessage { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public RepositoryScanResult? Result { get; init; }

    public static RepositoryScanProgressUpdate CreatePending(
        string owner,
        string repository,
        DateTimeOffset updatedAtUtc) =>
        new()
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Pending,
            Message = "Scan queued.",
            PercentComplete = 0,
            UpdatedAtUtc = updatedAtUtc,
        };

    public static RepositoryScanProgressUpdate CreateRunningPageProgress(
        string owner,
        string repository,
        GitHubContentReadProgress progress,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new RepositoryScanProgressUpdate
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Running,
            Message = CreateRunningMessage(progress.CurrentContentKind, progress.PageNumber),
            CurrentPageNumber = progress.PageNumber,
            CurrentContentKind = progress.CurrentContentKind,
            CurrentPageItemCount = progress.PageItemCount,
            PullRequestsRead = progress.PullRequestsRead,
            IssuesRead = progress.IssuesRead,
            TotalItemsRead = progress.ItemsRead,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    public static RepositoryScanProgressUpdate FromPersistedScan(RepositoryScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);

        return scan.Status switch
        {
            RepositoryScanStatuses.Completed => CreateCompletedUpdate(scan),
            RepositoryScanStatuses.Failed => CreateFailedUpdate(scan),
            RepositoryScanStatuses.Running => new RepositoryScanProgressUpdate
            {
                RepositoryOwner = scan.RepositoryOwner,
                RepositoryName = scan.RepositoryName,
                NormalizedKey = scan.NormalizedKey,
                Status = RepositoryScanStatuses.Running,
                Message = "Scanning repository content...",
                PercentComplete = 0,
                UpdatedAtUtc = ToUtcOffset(scan.UpdatedAtUtc),
            },
            _ => CreatePending(
                scan.RepositoryOwner,
                scan.RepositoryName,
                ToUtcOffset(scan.UpdatedAtUtc)),
        };
    }

    public static string CreateGroupName(string owner, string repository) =>
        $"repository-scan:{RepositoryScan.CreateNormalizedKey(owner, repository)}";

    private static RepositoryScanProgressUpdate CreateCompletedUpdate(RepositoryScan scan)
    {
        if (string.IsNullOrWhiteSpace(scan.ResultJson))
        {
            throw new InvalidOperationException("A completed repository scan must include serialized results.");
        }

        var result = RepositoryScanResultSerializer.Deserialize(scan.ResultJson);
        var completedAtUtc = ToUtcOffset(scan.CompletedAtUtc ?? scan.UpdatedAtUtc);

        return new RepositoryScanProgressUpdate
        {
            RepositoryOwner = result.RepositoryOwner,
            RepositoryName = result.RepositoryName,
            NormalizedKey = scan.NormalizedKey,
            Status = RepositoryScanStatuses.Completed,
            Message = CreateCompletionMessage(result.PullRequestSummary.ItemCount, result.IssueSummary.ItemCount),
            PullRequestsRead = result.PullRequestSummary.ItemCount,
            IssuesRead = result.IssueSummary.ItemCount,
            TotalItemsRead = result.RepositorySummary.ItemCount,
            PercentComplete = 100,
            UpdatedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Result = result,
        };
    }

    private static RepositoryScanProgressUpdate CreateFailedUpdate(RepositoryScan scan)
    {
        var completedAtUtc = ToUtcOffset(scan.CompletedAtUtc ?? scan.UpdatedAtUtc);
        var failureMessage = string.IsNullOrWhiteSpace(scan.FailureMessage)
            ? "The repository scan failed."
            : scan.FailureMessage;

        return new RepositoryScanProgressUpdate
        {
            RepositoryOwner = scan.RepositoryOwner,
            RepositoryName = scan.RepositoryName,
            NormalizedKey = scan.NormalizedKey,
            Status = RepositoryScanStatuses.Failed,
            Message = "Scan failed.",
            FailureMessage = failureMessage,
            UpdatedAtUtc = completedAtUtc,
            CompletedAtUtc = completedAtUtc,
        };
    }

    private static string CreateCompletionMessage(int pullRequestCount, int issueCount)
    {
        if (pullRequestCount <= 0 && issueCount <= 0)
        {
            return "Scan completed.";
        }

        if (issueCount <= 0)
        {
            return pullRequestCount == 1
                ? "Scan completed after processing 1 pull request."
                : $"Scan completed after processing {pullRequestCount} pull requests.";
        }

        if (pullRequestCount <= 0)
        {
            return issueCount == 1
                ? "Scan completed after processing 1 issue."
                : $"Scan completed after processing {issueCount} issues.";
        }

        var pullRequestLabel = pullRequestCount == 1 ? "pull request" : "pull requests";
        var issueLabel = issueCount == 1 ? "issue" : "issues";

        return $"Scan completed after processing {pullRequestCount} {pullRequestLabel} and {issueCount} {issueLabel}.";
    }

    private static string CreateRunningMessage(GitHubContentKind currentContentKind, int pageNumber) =>
        currentContentKind switch
        {
            GitHubContentKind.Issue => $"Fetched issue page {pageNumber}.",
            _ => $"Fetched pull request page {pageNumber}."
        };

    private static DateTimeOffset ToUtcOffset(DateTime utcDateTime) =>
        new(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
}
