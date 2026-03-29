namespace EmojiEstimator.Web.Services;

public sealed class RepositoryContentSummary
{
    public static RepositoryContentSummary Empty { get; } = new();

    public int ItemCount { get; init; }

    public int ItemsWithEmojiCount { get; init; }

    public int TotalEmojiCount { get; init; }

    public decimal AverageEmojisPerItem { get; init; }

    public int ItemsWithEmDashCount { get; init; }

    public int TotalEmDashCount { get; init; }

    public decimal AverageEmDashesPerItem { get; init; }

    public static RepositoryContentSummary Create(
        int itemCount,
        int itemsWithEmojiCount,
        int totalEmojiCount,
        int itemsWithEmDashCount,
        int totalEmDashCount) =>
        new()
        {
            ItemCount = itemCount,
            ItemsWithEmojiCount = itemsWithEmojiCount,
            TotalEmojiCount = totalEmojiCount,
            AverageEmojisPerItem = itemCount == 0 ? 0m : totalEmojiCount / (decimal)itemCount,
            ItemsWithEmDashCount = itemsWithEmDashCount,
            TotalEmDashCount = totalEmDashCount,
            AverageEmDashesPerItem = itemCount == 0 ? 0m : totalEmDashCount / (decimal)itemCount,
        };

    public static RepositoryContentSummary Combine(
        RepositoryContentSummary first,
        RepositoryContentSummary second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return Create(
            first.ItemCount + second.ItemCount,
            first.ItemsWithEmojiCount + second.ItemsWithEmojiCount,
            first.TotalEmojiCount + second.TotalEmojiCount,
            first.ItemsWithEmDashCount + second.ItemsWithEmDashCount,
            first.TotalEmDashCount + second.TotalEmDashCount);
    }
}
