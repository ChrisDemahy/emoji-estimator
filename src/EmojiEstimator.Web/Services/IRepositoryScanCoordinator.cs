namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanCoordinator
{
    Task<RepositoryScanProgressUpdate?> GetCurrentStateAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);

    Task<RepositoryScanProgressUpdate> QueueScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);
}
