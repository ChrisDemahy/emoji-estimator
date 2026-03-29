using EmojiEstimator.Web.Controllers;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Models;
using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmojiEstimator.UnitTests;

public sealed class RepositoryControllerTests
{
    [Fact]
    public async Task IndexReturnsRepositoryPageModelWhenNoCachedResultExists()
    {
        var controller = new RepositoryController(new StubRepositoryScanCoordinator());

        var result = await controller.Index("dotnet", "aspnetcore", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RepositoryPageViewModel>(viewResult.Model);
        Assert.Equal("dotnet", model.RepositoryOwner);
        Assert.Equal("aspnetcore", model.RepositoryName);
        Assert.Equal("DOTNET/ASPNETCORE", model.NormalizedKey);
        Assert.Equal("/dotnet/aspnetcore", model.RoutePath);
        Assert.Equal("/dotnet/aspnetcore/live-updates", model.LiveUpdatesUrl);
        Assert.Equal("/dotnet/aspnetcore/ensure-scan", model.EnsureScanUrl);
        Assert.True(model.ShouldEnsureScan);
        Assert.False(model.HasCompletedResult);
        Assert.Null(model.InitialUpdate);
        Assert.Equal("null", model.InitialUpdateJson);
    }

    [Fact]
    public async Task IndexUsesFreshCompletedResultWithoutQueuingAnotherScan()
    {
        var completedState = new RepositoryScanProgressUpdate
        {
            RepositoryOwner = "dotnet",
            RepositoryName = "aspnetcore",
            NormalizedKey = "DOTNET/ASPNETCORE",
            Status = RepositoryScanStatuses.Completed,
            Message = "Scan completed after processing 12 pull requests and 8 issues.",
            PullRequestsRead = 12,
            IssuesRead = 8,
            TotalItemsRead = 20,
            PercentComplete = 100,
            UpdatedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
            Result = new RepositoryScanResult
            {
                RepositoryOwner = "dotnet",
                RepositoryName = "aspnetcore",
                PullRequestCount = 12,
                PullRequestsWithEmojiCount = 5,
                TotalEmojiCount = 18,
                AverageEmojisPerPullRequest = 1.5m,
                PullRequestSummary = RepositoryContentSummary.Create(12, 5, 18, 4, 6),
                IssueSummary = RepositoryContentSummary.Create(8, 3, 9, 2, 2),
                RepositorySummary = RepositoryContentSummary.Create(20, 8, 27, 6, 8),
                ScannedAtUtc = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero)
            }
        };
        var controller = new RepositoryController(new StubRepositoryScanCoordinator(completedState));

        var result = await controller.Index(" dotnet ", " aspnetcore ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<RepositoryPageViewModel>(viewResult.Model);
        Assert.Equal("dotnet", model.RepositoryOwner);
        Assert.Equal("aspnetcore", model.RepositoryName);
        Assert.Equal("/dotnet/aspnetcore/live-updates", model.LiveUpdatesUrl);
        Assert.Equal("/dotnet/aspnetcore/ensure-scan", model.EnsureScanUrl);
        Assert.False(model.ShouldEnsureScan);
        Assert.True(model.HasCompletedResult);
        Assert.Same(completedState, model.InitialUpdate);
        Assert.Contains("\"status\":\"Completed\"", model.InitialUpdateJson, StringComparison.Ordinal);
        Assert.Contains("\"pullRequestSummary\":{\"itemCount\":12", model.InitialUpdateJson, StringComparison.Ordinal);
        Assert.Contains("\"issueSummary\":{\"itemCount\":8", model.InitialUpdateJson, StringComparison.Ordinal);
        Assert.Contains("\"repositorySummary\":{\"itemCount\":20", model.InitialUpdateJson, StringComparison.Ordinal);
    }

    private sealed class StubRepositoryScanCoordinator(RepositoryScanProgressUpdate? currentState = null) : IRepositoryScanCoordinator
    {
        public Task<RepositoryScanProgressUpdate?> GetCurrentStateAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(currentState);

        public Task<RepositoryScanProgressUpdate> QueueScanAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
