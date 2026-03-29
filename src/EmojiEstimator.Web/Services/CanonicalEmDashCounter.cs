namespace EmojiEstimator.Web.Services;

public sealed class CanonicalEmDashCounter : IEmDashCounter
{
    private const char EmDash = '\u2014';

    public int CountEmDashes(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var emDashCount = 0;

        foreach (var character in text)
        {
            if (character == EmDash)
            {
                emDashCount++;
            }
        }

        return emDashCount;
    }
}
