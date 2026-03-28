namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanner
{
    Task<RepositoryScanResult> ScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default);
}
