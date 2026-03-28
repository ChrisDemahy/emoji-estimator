using System.Text.Json;
using EmojiEstimator.Web.Data;
using EmojiEstimator.Web.Models;
using EmojiEstimator.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmojiEstimator.Web.Controllers;

public sealed class RepositoryController(IRepositoryScanCoordinator scanCoordinator) : Controller
{
    private static readonly JsonSerializerOptions InitialUpdateJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("{username}/{repository}")]
    public async Task<IActionResult> Index(string username, string repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(repository))
        {
            return NotFound();
        }

        var trimmedOwner = username.Trim();
        var trimmedRepository = repository.Trim();
        var currentState = await scanCoordinator.GetCurrentStateAsync(trimmedOwner, trimmedRepository, cancellationToken);

        var repositoryOwner = currentState?.RepositoryOwner ?? trimmedOwner;
        var repositoryName = currentState?.RepositoryName ?? trimmedRepository;

        return View(new RepositoryPageViewModel
        {
            RepositoryOwner = repositoryOwner,
            RepositoryName = repositoryName,
            NormalizedKey = currentState?.NormalizedKey ?? RepositoryScan.CreateNormalizedKey(repositoryOwner, repositoryName),
            RoutePath = $"/{repositoryOwner}/{repositoryName}",
            InitialUpdate = currentState,
            InitialUpdateJson = SerializeInitialUpdate(currentState),
            ShouldEnsureScan = !HasCompletedResult(currentState)
        });
    }

    private static bool HasCompletedResult(RepositoryScanProgressUpdate? update) =>
        update is { Result: not null } &&
        string.Equals(update.Status, RepositoryScanStatuses.Completed, StringComparison.Ordinal);

    private static string SerializeInitialUpdate(RepositoryScanProgressUpdate? update) =>
        update is null
            ? "null"
            : JsonSerializer.Serialize(update, InitialUpdateJsonOptions);
}
