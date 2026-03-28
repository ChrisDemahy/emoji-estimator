namespace EmojiEstimator.Web.Data;

public sealed class RepositoryScan
{
    public long Id { get; set; }

    public string RepositoryOwner { get; set; } = string.Empty;

    public string RepositoryName { get; set; } = string.Empty;

    public string NormalizedKey { get; set; } = string.Empty;

    public string Status { get; set; } = RepositoryScanStatuses.Pending;

    public string? ResultJson { get; set; }

    public string? FailureMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsFreshAt(DateTime utcNow) =>
        string.Equals(Status, RepositoryScanStatuses.Completed, StringComparison.Ordinal) &&
        ExpiresAtUtc is DateTime expiresAtUtc &&
        expiresAtUtc > utcNow;

    public bool IsStaleAt(DateTime utcNow, TimeSpan freshnessWindow)
    {
        var staleAfterUtc = ExpiresAtUtc ?? UpdatedAtUtc.Add(freshnessWindow);
        return staleAfterUtc <= utcNow;
    }

    public static string CreateNormalizedKey(string owner, string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        return $"{owner.Trim().ToUpperInvariant()}/{repository.Trim().ToUpperInvariant()}";
    }
}
