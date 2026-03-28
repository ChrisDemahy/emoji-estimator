using EmojiEstimator.Web.Services;

namespace EmojiEstimator.UnitTests;

public sealed class UnicodeEmojiCounterTests
{
    private static readonly IEmojiCounter Counter = new UnicodeEmojiCounter();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CountEmojis_ReturnsZeroForMissingBodyText(string? body)
    {
        Assert.Equal(0, Counter.CountEmojis(body));
    }

    [Fact]
    public void CountEmojis_CountsSingleAndCompositeEmojiClusters()
    {
        var emojiCount = Counter.CountEmojis("Ship it 😀👍🏽👨‍👩‍👧‍👦🇺🇸1️⃣❤️‍🔥");

        Assert.Equal(6, emojiCount);
    }

    [Fact]
    public void CountEmojis_IgnoresPlainTextAndUnstyledSymbols()
    {
        var emojiCount = Counter.CountEmojis("Version 1.2.3 ACME™ uses # for headings.");

        Assert.Equal(0, emojiCount);
    }
}
