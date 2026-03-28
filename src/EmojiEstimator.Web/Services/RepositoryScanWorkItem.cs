namespace EmojiEstimator.Web.Services;

public sealed record RepositoryScanWorkItem(
    string RepositoryOwner,
    string RepositoryName,
    string NormalizedKey);
