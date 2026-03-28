using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace EmojiEstimator.Web.Hubs;

public sealed class RepositoryScanHub(IRepositoryScanCoordinator scanCoordinator) : Hub<IRepositoryScanClient>
{
    public async Task SubscribeAsync(string owner, string repository)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RepositoryScanProgressUpdate.CreateGroupName(owner, repository),
            Context.ConnectionAborted);

        var currentState = await scanCoordinator.GetCurrentStateAsync(
            owner,
            repository,
            Context.ConnectionAborted);

        if (currentState is not null)
        {
            await Clients.Caller.ScanUpdated(currentState);
        }
    }

    public Task UnsubscribeAsync(string owner, string repository) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RepositoryScanProgressUpdate.CreateGroupName(owner, repository),
            Context.ConnectionAborted);

    public Task<RepositoryScanProgressUpdate> EnsureScanAsync(string owner, string repository) =>
        scanCoordinator.QueueScanAsync(owner, repository, Context.ConnectionAborted);
}
