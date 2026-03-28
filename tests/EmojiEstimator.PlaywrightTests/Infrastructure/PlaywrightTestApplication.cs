using EmojiEstimator.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmojiEstimator.PlaywrightTests.Infrastructure;

public sealed class PlaywrightTestApplication : IAsyncDisposable
{
    private readonly PlaywrightWebApplicationFactory factory;
    private bool disposed;

    private PlaywrightTestApplication(PlaywrightWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    public Uri BaseAddress => factory.RootUri;

    public MutableTimeProvider Clock => factory.AppServices.GetRequiredService<MutableTimeProvider>();

    public TestGitHubScenarioStore GitHubScenarios => factory.AppServices.GetRequiredService<TestGitHubScenarioStore>();

    public static Task<PlaywrightTestApplication> StartAsync()
    {
        var factory = new PlaywrightWebApplicationFactory();
        factory.EnsureStarted();
        return Task.FromResult(new PlaywrightTestApplication(factory));
    }

    public async Task SeedScanAsync(RepositoryScan scan)
    {
        await using var scope = factory.AppServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EmojiEstimatorDbContext>();
        var existingScan = await dbContext.RepositoryScans.SingleOrDefaultAsync(
            repositoryScan => repositoryScan.NormalizedKey == scan.NormalizedKey);

        if (existingScan is not null)
        {
            dbContext.RepositoryScans.Remove(existingScan);
            await dbContext.SaveChangesAsync();
        }

        dbContext.RepositoryScans.Add(scan);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> GetRepositoryScanCountAsync()
    {
        await using var scope = factory.AppServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EmojiEstimatorDbContext>();
        return await dbContext.RepositoryScans.CountAsync();
    }

    public async Task<RepositoryScan> GetRequiredScanAsync(string owner, string repository)
    {
        await using var scope = factory.AppServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EmojiEstimatorDbContext>();
        var normalizedKey = RepositoryScan.CreateNormalizedKey(owner, repository);
        var scan = await dbContext.RepositoryScans.SingleOrDefaultAsync(
            repositoryScan => repositoryScan.NormalizedKey == normalizedKey);

        return scan ?? throw new InvalidOperationException($"No repository scan exists for {owner}/{repository}.");
    }

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            factory.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
