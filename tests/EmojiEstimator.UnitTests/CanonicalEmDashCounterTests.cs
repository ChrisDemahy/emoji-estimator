using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class CanonicalEmDashCounterTests
{
    private static readonly IEmDashCounter Counter = new CanonicalEmDashCounter();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CountEmDashes_ReturnsZeroForMissingBodyText(string? body)
    {
        Assert.Equal(0, Counter.CountEmDashes(body));
    }

    [Fact]
    public void CountEmDashes_CountsEmDashesInMixedText()
    {
        var emDashCount = Counter.CountEmDashes("Status — ready. Review—approved. Done.");

        Assert.Equal(2, emDashCount);
    }

    [Fact]
    public void CountEmDashes_CountsRepeatedEmDashes()
    {
        var emDashCount = Counter.CountEmDashes("Wait——what——now?");

        Assert.Equal(4, emDashCount);
    }

    [Fact]
    public void CountEmDashes_IgnoresOtherPunctuation()
    {
        var emDashCount = Counter.CountEmDashes("Hyphen - en dash – figure dash ‒ horizontal bar ― minus −.");

        Assert.Equal(0, emDashCount);
    }

    [Fact]
    public void CountEmDashes_OnlyCountsCanonicalEmDashesAcrossSimilarCharacters()
    {
        var emDashCount = Counter.CountEmDashes("Hyphen - non-breaking hyphen ‑ en dash – em dash — horizontal bar ―.");

        Assert.Equal(1, emDashCount);
    }

    [Fact]
    public void CountEmDashes_HandlesLargeNumbersOfEmDashes()
    {
        var body = string.Concat(Enumerable.Repeat("—", 512));

        Assert.Equal(512, Counter.CountEmDashes(body));
    }
}
