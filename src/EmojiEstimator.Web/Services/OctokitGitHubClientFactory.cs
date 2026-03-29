using Octokit;

namespace EmojiEstimator.Web.Services;

internal static class OctokitGitHubClientFactory
{
    private static readonly ProductHeaderValue ProductHeader = new("emoji-estimator");

    public static GitHubClient CreateClient(GitHubOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var token = options.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                $"GitHub configuration is invalid. Set {GitHubOptions.SectionName}:Token to a personal access token.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                $"GitHub configuration is invalid. {GitHubOptions.SectionName}:BaseUrl must be an absolute URI.");
        }

        return new GitHubClient(ProductHeader, baseUri)
        {
            Credentials = new Credentials(token),
        };
    }
}
