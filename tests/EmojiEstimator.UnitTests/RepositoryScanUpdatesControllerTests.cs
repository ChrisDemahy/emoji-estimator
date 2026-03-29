using System.Text;
using System.Threading.Channels;
using EmojiEstimator.Web.Controllers;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryScanUpdatesControllerTests
{
    [Fact]
    public async Task LiveUpdates_WritesConnectedCommentWhenNoCurrentStateExists()
    {
        var coordinator = new StubRepositoryScanCoordinator();
        using var notifier = new StubRepositoryScanProgressNotifier();
        var controller = CreateController(coordinator, notifier);

        await controller.LiveUpdates(" octocat ", " hello-world ", CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Equal("text/event-stream", controller.Response.Headers[HeaderNames.ContentType]);
        Assert.Equal("no-cache, no-store", controller.Response.Headers[HeaderNames.CacheControl]);
        Assert.Equal("no", controller.Response.Headers["X-Accel-Buffering"]);
        Assert.Equal("OCTOCAT/HELLO-WORLD", notifier.SubscribedKey);
        Assert.Equal("octocat", coordinator.CurrentStateOwner);
        Assert.Equal("hello-world", coordinator.CurrentStateRepository);
        Assert.Equal(": connected\n\n", GetResponseText(controller));
    }

    [Fact]
    public async Task LiveUpdates_WritesCurrentStateAndStreamedUpdatesAsServerSentEvents()
    {
        var initialState = CreateUpdate(
            status: RepositoryScanStatuses.Completed,
            message: "Scan completed after processing 2 pull requests and 1 issue.",
            result: new RepositoryScanResult
            {
                RepositoryOwner = "octocat",
                RepositoryName = "hello-world",
                PullRequestCount = 2,
                PullRequestsWithEmojiCount = 1,
                TotalEmojiCount = 3,
                AverageEmojisPerPullRequest = 1.5m,
                PullRequestSummary = RepositoryContentSummary.Create(2, 1, 3, 2, 4),
                IssueSummary = RepositoryContentSummary.Create(1, 1, 1, 1, 2),
                RepositorySummary = RepositoryContentSummary.Create(3, 2, 4, 3, 6),
                ScannedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
            });
        var streamedUpdate = CreateUpdate(
            status: RepositoryScanStatuses.Running,
            message: "Fetched issue page 2.");
        var coordinator = new StubRepositoryScanCoordinator(initialState);
        using var notifier = new StubRepositoryScanProgressNotifier(streamedUpdate);
        var controller = CreateController(coordinator, notifier);

        await controller.LiveUpdates(" octocat ", " hello-world ", CancellationToken.None);

        Assert.Equal("OCTOCAT/HELLO-WORLD", notifier.SubscribedKey);
        Assert.Equal("octocat", coordinator.CurrentStateOwner);
        Assert.Equal("hello-world", coordinator.CurrentStateRepository);

        var responseText = GetResponseText(controller);
        Assert.Contains("event: scan-update", responseText, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Completed\"", responseText, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Running\"", responseText, StringComparison.Ordinal);
        Assert.Contains("\"pullRequestSummary\":{\"itemCount\":2", responseText, StringComparison.Ordinal);
        Assert.Contains("\"issueSummary\":{\"itemCount\":1", responseText, StringComparison.Ordinal);
        Assert.Contains("\"repositorySummary\":{\"itemCount\":3", responseText, StringComparison.Ordinal);
        Assert.Contains("\"totalEmDashCount\":6", responseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiveUpdates_ReturnsNotFoundForMissingRouteValues()
    {
        var controller = CreateController(
            new StubRepositoryScanCoordinator(),
            new StubRepositoryScanProgressNotifier());

        await controller.LiveUpdates(" ", "hello-world", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal(string.Empty, GetResponseText(controller));
    }

    [Fact]
    public async Task EnsureScan_TrimsRouteValuesAndReturnsQueuedUpdate()
    {
        var queuedUpdate = CreateUpdate(status: RepositoryScanStatuses.Pending, message: "Scan queued.");
        var coordinator = new StubRepositoryScanCoordinator(queuedUpdate);
        var controller = CreateController(
            coordinator,
            new StubRepositoryScanProgressNotifier());

        var actionResult = await controller.EnsureScan(" octocat ", " hello-world ", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var returnedUpdate = Assert.IsType<RepositoryScanProgressUpdate>(okResult.Value);
        Assert.Same(queuedUpdate, returnedUpdate);
        Assert.Equal("octocat", coordinator.QueuedOwner);
        Assert.Equal("hello-world", coordinator.QueuedRepository);
    }

    [Theory]
    [InlineData("", "hello-world")]
    [InlineData("octocat", " ")]
    public async Task EnsureScan_ReturnsNotFoundForMissingRouteValues(string username, string repository)
    {
        var controller = CreateController(
            new StubRepositoryScanCoordinator(),
            new StubRepositoryScanProgressNotifier());

        var actionResult = await controller.EnsureScan(username, repository, CancellationToken.None);

        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    private static RepositoryScanUpdatesController CreateController(
        IRepositoryScanCoordinator coordinator,
        IRepositoryScanProgressNotifier notifier)
    {
        var controller = new RepositoryScanUpdatesController(coordinator, notifier)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Response.Body = new MemoryStream();
        return controller;
    }

    private static string GetResponseText(Controller controller)
    {
        controller.Response.Body.Position = 0;
        using var reader = new StreamReader(controller.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static RepositoryScanProgressUpdate CreateUpdate(
        string status,
        string message,
        RepositoryScanResult? result = null) =>
        new()
        {
            RepositoryOwner = "octocat",
            RepositoryName = "hello-world",
            NormalizedKey = "OCTOCAT/HELLO-WORLD",
            Status = status,
            Message = message,
            PullRequestsRead = result?.PullRequestSummary.ItemCount ?? 2,
            IssuesRead = result?.IssueSummary.ItemCount ?? 1,
            TotalItemsRead = result?.RepositorySummary.ItemCount ?? 3,
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = status == RepositoryScanStatuses.Completed
                ? new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero)
                : null,
            Result = result
        };

    private sealed class StubRepositoryScanCoordinator(RepositoryScanProgressUpdate? update = null) : IRepositoryScanCoordinator
    {
        public string? CurrentStateOwner { get; private set; }

        public string? CurrentStateRepository { get; private set; }

        public string? QueuedOwner { get; private set; }

        public string? QueuedRepository { get; private set; }

        public Task<RepositoryScanProgressUpdate?> GetCurrentStateAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            CurrentStateOwner = owner;
            CurrentStateRepository = repository;
            return Task.FromResult(update);
        }

        public Task<RepositoryScanProgressUpdate> QueueScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default)
        {
            QueuedOwner = owner;
            QueuedRepository = repository;
            return Task.FromResult(update ?? throw new InvalidOperationException("No queued update was configured."));
        }
    }

    private sealed class StubRepositoryScanProgressNotifier(params RepositoryScanProgressUpdate[] queuedUpdates) : IRepositoryScanProgressNotifier, IDisposable
    {
        private readonly Channel<RepositoryScanProgressUpdate> channel = Channel.CreateUnbounded<RepositoryScanProgressUpdate>();

        public string? SubscribedKey { get; private set; }

        public RepositoryScanProgressUpdate? GetLatest(string normalizedKey) => null;

        public RepositoryScanProgressSubscription Subscribe(string normalizedKey)
        {
            SubscribedKey = normalizedKey;

            foreach (RepositoryScanProgressUpdate update in queuedUpdates)
            {
                channel.Writer.TryWrite(update);
            }

            channel.Writer.TryComplete();
            return new RepositoryScanProgressSubscription(channel.Reader, () => { });
        }

        public void Store(RepositoryScanProgressUpdate update)
        {
        }

        public void Clear(string normalizedKey)
        {
        }

        public Task PublishAsync(
            RepositoryScanProgressUpdate update,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            channel.Writer.TryComplete();
        }
    }
}
