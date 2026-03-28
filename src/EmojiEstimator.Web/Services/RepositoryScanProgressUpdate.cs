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

    public int? PullRequestsRead { get; init; }

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
        GitHubPullRequestReadProgress progress,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new RepositoryScanProgressUpdate
        {
            RepositoryOwner = owner,
            RepositoryName = repository,
            NormalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository),
            Status = RepositoryScanStatuses.Running,
            Message = $"Fetched pull request page {progress.PageNumber}.",
            CurrentPageNumber = progress.PageNumber,
            PullRequestsRead = progress.PullRequestsRead,
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
                Message = "Scanning pull requests...",
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
            Message = CreateCompletionMessage(result.PullRequestCount),
            PullRequestsRead = result.PullRequestCount,
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

    private static string CreateCompletionMessage(int pullRequestCount) =>
        pullRequestCount == 1
            ? "Scan completed after processing 1 pull request."
            : $"Scan completed after processing {pullRequestCount} pull requests.";

    private static DateTimeOffset ToUtcOffset(DateTime utcDateTime) =>
        new(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
}
