using EmojiEstimator.Web.Services;
using Microsoft.Extensions.Options;

namespace EmojiEstimator.UnitTests;

public sealed class OctokitGitHubPullRequestPageSourceTests
{
    [Fact]
    public void Constructor_ThrowsWhenTokenIsMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new OctokitGitHubPullRequestPageSource(Options.Create(new GitHubOptions())));

        Assert.Contains("GitHub:Token", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsWhenBaseUrlIsInvalid()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new OctokitGitHubPullRequestPageSource(Options.Create(new GitHubOptions
            {
                Token = "github-token",
                BaseUrl = "not-a-uri",
            })));

        Assert.Contains("GitHub:BaseUrl", exception.Message);
    }
}
