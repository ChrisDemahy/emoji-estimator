namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanBackgroundQueue
{
    ValueTask QueueAsync(
        RepositoryScanWorkItem workItem,
        CancellationToken cancellationToken = default);

    ValueTask<RepositoryScanWorkItem> DequeueAsync(CancellationToken cancellationToken);
}
