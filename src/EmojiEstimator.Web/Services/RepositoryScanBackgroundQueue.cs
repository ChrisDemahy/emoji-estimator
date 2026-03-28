using System.Threading.Channels;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanBackgroundQueue : IRepositoryScanBackgroundQueue
{
    private readonly Channel<RepositoryScanWorkItem> channel = Channel.CreateUnbounded<RepositoryScanWorkItem>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = false,
            SingleWriter = false,
        });

    public ValueTask QueueAsync(
        RepositoryScanWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return channel.Writer.WriteAsync(workItem, cancellationToken);
    }

    public ValueTask<RepositoryScanWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);
}
