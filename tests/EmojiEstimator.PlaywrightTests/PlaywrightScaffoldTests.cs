using Microsoft.Playwright;

namespace EmojiEstimator.PlaywrightTests;

public sealed class PlaywrightScaffoldTests
{
    [Test]
    public void PlaywrightPackageIsAvailable()
    {
        Assert.That(typeof(IPlaywright).Assembly.GetName().Name, Is.EqualTo("Microsoft.Playwright"));
    }
}
