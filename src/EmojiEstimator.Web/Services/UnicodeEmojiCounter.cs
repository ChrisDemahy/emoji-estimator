using System.Globalization;
using System.Text;

namespace EmojiEstimator.Web.Services;

public sealed class UnicodeEmojiCounter : IEmojiCounter
{
    public int CountEmojis(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var emojiCount = 0;
        var textElementEnumerator = StringInfo.GetTextElementEnumerator(text);

        while (textElementEnumerator.MoveNext())
        {
            if (textElementEnumerator.GetTextElement() is string textElement &&
                IsEmojiTextElement(textElement))
            {
                emojiCount++;
            }
        }

        return emojiCount;
    }

    private static bool IsEmojiTextElement(string textElement)
    {
        var hasVariationSelector16 = false;
        var hasVariationEligibleBase = false;
        var hasKeycapBase = false;
        var hasKeycapCombiningMark = false;
        var regionalIndicatorCount = 0;

        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;

            if (value == 0xFE0F)
            {
                hasVariationSelector16 = true;
                continue;
            }

            if (value == 0x200D || IsEmojiModifier(rune) || IsTagRune(rune))
            {
                continue;
            }

            if (value == 0x20E3)
            {
                hasKeycapCombiningMark = true;
                continue;
            }

            if (IsKeycapBase(rune))
            {
                hasKeycapBase = true;
                continue;
            }

            if (IsRegionalIndicator(rune))
            {
                regionalIndicatorCount++;
                continue;
            }

            if (IsDefaultEmojiRune(rune))
            {
                return true;
            }

            if (IsVariationEligibleEmojiRune(rune))
            {
                hasVariationEligibleBase = true;
            }
        }

        if (regionalIndicatorCount == 2 || (hasKeycapBase && hasKeycapCombiningMark))
        {
            return true;
        }

        return hasVariationSelector16 && hasVariationEligibleBase;
    }

    private static bool IsDefaultEmojiRune(Rune rune)
    {
        var value = rune.Value;

        return value switch
        {
            >= 0x1F000 and <= 0x1FAFF => true,
            >= 0x2600 and <= 0x27BF => true,
            0x231A or 0x231B or
            0x23E9 or 0x23EA or 0x23EB or 0x23EC or
            0x23F0 or 0x23F3 or
            0x24C2 or
            0x25AA or 0x25AB or 0x25B6 or 0x25C0 or 0x25FB or 0x25FC or 0x25FD or 0x25FE or
            0x2B50 or 0x2B55 => true,
            _ => false,
        };
    }

    private static bool IsVariationEligibleEmojiRune(Rune rune)
    {
        var value = rune.Value;

        return value switch
        {
            0x00A9 or 0x00AE or
            0x203C or 0x2049 or
            0x2122 or 0x2139 or
            0x2194 or 0x2195 or 0x2196 or 0x2197 or 0x2198 or 0x2199 or
            0x21A9 or 0x21AA or
            0x2328 or 0x23CF or 0x23ED or 0x23EE or 0x23EF or 0x23F1 or 0x23F2 or
            0x2934 or 0x2935 or
            0x2B05 or 0x2B06 or 0x2B07 or 0x2B1B or 0x2B1C or
            0x3030 or 0x303D or
            0x3297 or 0x3299 => true,
            _ => false,
        };
    }

    private static bool IsEmojiModifier(Rune rune) => rune.Value is >= 0x1F3FB and <= 0x1F3FF;

    private static bool IsKeycapBase(Rune rune)
    {
        var value = rune.Value;
        return value == 0x23 || value == 0x2A || (value >= 0x30 && value <= 0x39);
    }

    private static bool IsRegionalIndicator(Rune rune) => rune.Value is >= 0x1F1E6 and <= 0x1F1FF;

    private static bool IsTagRune(Rune rune) => rune.Value is >= 0xE0020 and <= 0xE007F;
}
