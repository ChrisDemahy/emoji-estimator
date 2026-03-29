using System.Threading.Channels;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanProgressSubscription(
    ChannelReader<RepositoryScanProgressUpdate> reader,
    Action dispose) : IDisposable
{
    private readonly Action dispose = dispose;
    private int disposed;

    public ChannelReader<RepositoryScanProgressUpdate> Reader { get; } = reader;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            dispose();
        }
    }
}
