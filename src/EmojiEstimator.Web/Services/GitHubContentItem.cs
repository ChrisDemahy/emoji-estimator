namespace EmojiEstimator.Web.Services;

public sealed record GitHubContentItem(int Number, GitHubContentKind Kind, string? Body)
{
    public static GitHubContentItem CreatePullRequest(int number, string? body) =>
        new(number, GitHubContentKind.PullRequest, body);

    public static GitHubContentItem CreateIssue(int number, string? body) =>
        new(number, GitHubContentKind.Issue, body);
}
