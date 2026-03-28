using EmojiEstimator.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EmojiEstimator.Web.Services;

public sealed class RepositoryScanStore(EmojiEstimatorDbContext dbContext, TimeProvider timeProvider) : IRepositoryScanStore
{
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromHours(24);
    private const int FailureMessageMaxLength = 2048;

    public async Task<RepositoryScan?> GetCurrentScanOrDeleteStaleAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        var scan = await GetScanAsync(owner, repository, cancellationToken);
        if (scan is null)
        {
            return null;
        }

        var utcNow = GetUtcNow();
        if (scan.IsStaleAt(utcNow, FreshnessWindow))
        {
            dbContext.RepositoryScans.Remove(scan);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        return scan;
    }

    public async Task<RepositoryScan?> GetFreshScanOrDeleteStaleAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
    {
        var scan = await GetCurrentScanOrDeleteStaleAsync(owner, repository, cancellationToken);
        if (scan is null)
        {
            return null;
        }

        return scan.IsFreshAt(GetUtcNow()) ? scan : null;
    }

    public Task<RepositoryScan> SavePendingScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        SaveScanAsync(
            owner,
            repository,
            RepositoryScanStatuses.Pending,
            resultJson: null,
            failureMessage: null,
            isTerminal: false,
            cancellationToken);

    public Task<RepositoryScan> SaveRunningScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken = default) =>
        SaveScanAsync(
            owner,
            repository,
            RepositoryScanStatuses.Running,
            resultJson: null,
            failureMessage: null,
            isTerminal: false,
            cancellationToken);

    public async Task<RepositoryScan> SaveCompletedScanAsync(
        string owner,
        string repository,
        string resultJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultJson);
        return await SaveScanAsync(
            owner,
            repository,
            RepositoryScanStatuses.Completed,
            resultJson,
            failureMessage: null,
            isTerminal: true,
            cancellationToken);
    }

    public Task<RepositoryScan> SaveFailedScanAsync(
        string owner,
        string repository,
        string failureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        return SaveScanAsync(
            owner,
            repository,
            RepositoryScanStatuses.Failed,
            resultJson: null,
            failureMessage: TruncateFailureMessage(failureMessage),
            isTerminal: true,
            cancellationToken);
    }

    private async Task<RepositoryScan> SaveScanAsync(
        string owner,
        string repository,
        string status,
        string? resultJson,
        string? failureMessage,
        bool isTerminal,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var trimmedOwner = owner.Trim();
        var trimmedRepository = repository.Trim();
        var normalizedKey = RepositoryScan.CreateNormalizedKey(trimmedOwner, trimmedRepository);
        var utcNow = GetUtcNow();
        var scan = await GetScanAsync(trimmedOwner, trimmedRepository, cancellationToken);

        if (scan is null)
        {
            scan = new RepositoryScan
            {
                CreatedAtUtc = utcNow,
                NormalizedKey = normalizedKey,
            };

            await dbContext.RepositoryScans.AddAsync(scan, cancellationToken);
        }

        scan.RepositoryOwner = trimmedOwner;
        scan.RepositoryName = trimmedRepository;
        scan.Status = status;
        scan.ResultJson = resultJson;
        scan.FailureMessage = failureMessage;
        scan.CompletedAtUtc = isTerminal ? utcNow : null;
        scan.UpdatedAtUtc = utcNow;
        scan.ExpiresAtUtc = isTerminal ? utcNow.Add(FreshnessWindow) : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return scan;
    }

    private Task<RepositoryScan?> GetScanAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken) =>
        dbContext.RepositoryScans.SingleOrDefaultAsync(
            existingScan => existingScan.NormalizedKey == RepositoryScan.CreateNormalizedKey(owner, repository),
            cancellationToken);

    private static string TruncateFailureMessage(string failureMessage)
    {
        var trimmedMessage = failureMessage.Trim();
        return trimmedMessage.Length <= FailureMessageMaxLength
            ? trimmedMessage
            : trimmedMessage[..FailureMessageMaxLength];
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
