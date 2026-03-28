namespace EmojiEstimator.UnitTests;

public sealed class ScaffoldTests
{
    [Fact]
    public void WebApplicationEntryPointIsDiscoverable()
    {
        Assert.Equal("EmojiEstimator.Web", typeof(global::Program).Assembly.GetName().Name);
    }
}
