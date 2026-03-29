using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;

namespace EmojiEstimator.Web.Models;

public sealed class RepositoryPageViewModel
{
    public required string RepositoryOwner { get; init; }

    public required string RepositoryName { get; init; }

    public required string NormalizedKey { get; init; }

    public required string RoutePath { get; init; }

    public required string LiveUpdatesUrl { get; init; }

    public required string EnsureScanUrl { get; init; }

    public required string InitialUpdateJson { get; init; }

    public RepositoryScanProgressUpdate? InitialUpdate { get; init; }

    public bool ShouldEnsureScan { get; init; }

    public bool HasCompletedResult =>
        InitialUpdate is { Result: not null } update &&
        string.Equals(update.Status, RepositoryScanStatuses.Completed, StringComparison.Ordinal);
}
