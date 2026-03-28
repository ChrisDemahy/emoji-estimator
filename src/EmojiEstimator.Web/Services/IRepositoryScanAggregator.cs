namespace EmojiEstimator.Web.Services;

public interface IRepositoryScanAggregator
{
    RepositoryScanResult Aggregate(
        string owner,
        string repository,
        IEnumerable<GitHubPullRequestBody> pullRequests);
}
