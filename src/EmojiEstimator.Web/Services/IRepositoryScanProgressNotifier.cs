namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanProgressNotifier
{
    RepositoryScanProgressUpdate? GetLatest(string normalizedKey);

    RepositoryScanProgressSubscription Subscribe(string normalizedKey);

    void Store(RepositoryScanProgressUpdate update);

    void Clear(string normalizedKey);

    Task PublishAsync(
        RepositoryScanProgressUpdate update,
        CancellationToken cancellationToken = default);
}
