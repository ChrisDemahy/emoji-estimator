using EmojiEstimator.Web.Services;

namespace EmojiEstimator.Web.Hubs;

public interface IRepositoryScanClient
{
    Task ScanUpdated(RepositoryScanProgressUpdate update);
}
