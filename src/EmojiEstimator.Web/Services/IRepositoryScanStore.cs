using EmojiEstimator.Web.Data;

namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanStore
{
    Task<RepositoryScan?> GetCurrentScanOrDeleteStaleAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    Task<RepositoryScan?> GetFreshScanOrDeleteStaleAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    Task<RepositoryScan> SavePendingScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    Task<RepositoryScan> SaveRunningScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    Task<RepositoryScan> SaveCompletedScanAsync(
        string owner,
        string repository,
        string resultJson,
        CancellationToken cancellationToken = default);

    Task<RepositoryScan> SaveFailedScanAsync(
        string owner,
        string repository,
        string failureMessage,
        CancellationToken cancellationToken = default);
}
