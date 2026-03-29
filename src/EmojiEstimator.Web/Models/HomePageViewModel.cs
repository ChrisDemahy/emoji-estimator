namespace EmojiEstimator.Web.Models;

public sealed class HomePageViewModel
{
    public required string RouteTemplate { get; init; }

    public required IReadOnlyList<string> ExampleRoutes { get; init; }
}
