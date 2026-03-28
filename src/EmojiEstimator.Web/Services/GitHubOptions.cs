namespace EmojiEstimator.Web.Services;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string BaseUrl { get; set; } = "https://api.github.com/";

    public string Token { get; set; } = string.Empty;
}
