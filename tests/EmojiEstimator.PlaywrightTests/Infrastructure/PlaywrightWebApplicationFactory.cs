using System.Net;
using System.Net.Sockets;
using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EmojiEstimator.PlaywrightTests.Infrastructure;

public sealed class PlaywrightWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath;
    private readonly Uri rootUri;
    private IHost? kestrelHost;

    public PlaywrightWebApplicationFactory()
    {
        var databaseDirectory = Path.Combine(AppContext.BaseDirectory, "TestData");
        Directory.CreateDirectory(databaseDirectory);
        databasePath = Path.Combine(databaseDirectory, $"emoji-estimator-playwright-{Guid.NewGuid():N}.db");
        rootUri = new Uri($"http://127.0.0.1:{GetFreePort()}/");
    }

    public IServiceProvider AppServices =>
        kestrelHost?.Services ?? throw new InvalidOperationException("The Playwright test application has not started.");

    public Uri RootUri =>
        ClientOptions.BaseAddress ?? throw new InvalidOperationException("The Playwright test application has not started.");

    public void EnsureStarted()
    {
        using var _ = CreateDefaultClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseStaticWebAssets();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ConnectionStrings:EmojiEstimatorDatabase", $"Data Source={databasePath}"),
                new KeyValuePair<string, string?>("GitHub:BaseUrl", "https://example.test/"),
                new KeyValuePair<string, string?>("GitHub:Token", "playwright-test-token")
            ]);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<IGitHubContentReader>();
            services.RemoveAll<IGitHubPullRequestPageSource>();
            services.RemoveAll<IGitHubIssuePageSource>();

            services.AddSingleton(new MutableTimeProvider(new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero)));
            services.AddSingleton<TimeProvider>(serviceProvider => serviceProvider.GetRequiredService<MutableTimeProvider>());
            services.AddSingleton<TestGitHubScenarioStore>();
            services.AddScoped<IGitHubContentReader, TestGitHubContentReader>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();
        testHost.Start();

        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseKestrel();
            webHostBuilder.UseUrls(rootUri.ToString());
        });

        kestrelHost = builder.Build();
        kestrelHost.Start();
        ClientOptions.BaseAddress = rootUri;
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            kestrelHost?.Dispose();
            kestrelHost = null;
        }

        base.Dispose(disposing);

        if (disposing)
        {
            DeleteDatabaseFiles();
        }
    }

    private void DeleteDatabaseFiles()
    {
        DeleteIfPresent(databasePath);
        DeleteIfPresent($"{databasePath}-shm");
        DeleteIfPresent($"{databasePath}-wal");
    }

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
