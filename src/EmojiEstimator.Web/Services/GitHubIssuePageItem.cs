namespace EmojiEstimator.Web.Services;

public sealed record GitHubIssuePageItem(int Number, string? Body, bool IsPullRequest)
{
    public static GitHubIssuePageItem CreateIssue(int number, string? body) =>
        new(number, body, false);

    public static GitHubIssuePageItem CreatePullRequest(int number, string? body) =>
        new(number, body, true);
}
