using System.Text.Json;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EmojiEstimator.Web.Controllers;

public sealed class RepositoryScanUpdatesController(
    IRepositoryScanCoordinator scanCoordinator,
    IRepositoryScanProgressNotifier progressNotifier) : Controller
{
    private static readonly JsonSerializerOptions UpdateJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    [HttpGet("{username}/{repository}/live-updates")]
    public async Task LiveUpdates(string username, string repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(repository))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        string trimmedOwner = username.Trim();
        string trimmedRepository = repository.Trim();
        string normalizedKey = RepositoryScan.CreateNormalizedKey(trimmedOwner, trimmedRepository);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers[HeaderNames.ContentType] = "text/event-stream";
        Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no";

        using RepositoryScanProgressSubscription subscription = progressNotifier.Subscribe(normalizedKey);
        RepositoryScanProgressUpdate? currentState = await scanCoordinator.GetCurrentStateAsync(
            trimmedOwner,
            trimmedRepository,
            cancellationToken);

        if (currentState is not null)
        {
            await WriteUpdateAsync(currentState, cancellationToken);
        }
        else
        {
            await WriteCommentAsync("connected", cancellationToken);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            Task<bool> waitForUpdateTask = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
            Task heartbeatTask = Task.Delay(HeartbeatInterval, cancellationToken);
            Task completedTask = await Task.WhenAny(waitForUpdateTask, heartbeatTask);

            if (ReferenceEquals(completedTask, heartbeatTask))
            {
                await WriteCommentAsync("keep-alive", cancellationToken);
                continue;
            }

            if (!await waitForUpdateTask)
            {
                break;
            }

            while (subscription.Reader.TryRead(out RepositoryScanProgressUpdate? update))
            {
                await WriteUpdateAsync(update, cancellationToken);
            }
        }
    }

    [HttpPost("{username}/{repository}/ensure-scan")]
    public async Task<ActionResult<RepositoryScanProgressUpdate>> EnsureScan(
        string username,
        string repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(repository))
        {
            return NotFound();
        }

        RepositoryScanProgressUpdate update = await scanCoordinator.QueueScanAsync(
            username.Trim(),
            repository.Trim(),
            cancellationToken);

        return Ok(update);
    }

    private async Task WriteCommentAsync(string comment, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($": {comment}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteUpdateAsync(
        RepositoryScanProgressUpdate update,
        CancellationToken cancellationToken)
    {
        string payload = JsonSerializer.Serialize(update, UpdateJsonOptions);
        await Response.WriteAsync($"event: scan-update\ndata: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
