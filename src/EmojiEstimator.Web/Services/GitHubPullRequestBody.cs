namespace EmojiEstimator.Web.Services;

public sealed class GitHubPullRequestBody
{
    public GitHubPullRequestBody(int number, string? body)
    {
        Number = number;
        Body = body;
    }

    public int Number { get; }

    public string? Body { get; }
}
