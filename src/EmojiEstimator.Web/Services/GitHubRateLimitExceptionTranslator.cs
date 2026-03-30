using Octokit;

namespace EmojiEstimator.Web.Services;

internal static class GitHubRateLimitExceptionTranslator
{
    private static readonly TimeSpan DefaultSecondaryRetryDelay = TimeSpan.FromSeconds(30);

    public static bool TryCreate(Exception exception, out GitHubRateLimitException? rateLimitException)
    {
        ArgumentNullException.ThrowIfNull(exception);

        rateLimitException = exception switch
        {
            GitHubRateLimitException existingException => existingException,
            RateLimitExceededException rateLimitExceededException => GitHubRateLimitException.CreateWithRetryAt(
                rateLimitExceededException.Reset,
                "GitHub primary rate limit exceeded. Waiting for the reset window before retrying.",
                rateLimitExceededException),
            AbuseException abuseException => CreateAbuseException(abuseException),
            SecondaryRateLimitExceededException secondaryRateLimitExceededException => CreateSecondaryRateLimitException(secondaryRateLimitExceededException),
            _ => null,
        };

        return rateLimitException is not null;
    }

    private static GitHubRateLimitException CreateAbuseException(AbuseException abuseException)
    {
        var retryAfter = abuseException.RetryAfterSeconds is int retryAfterSeconds && retryAfterSeconds > 0
            ? TimeSpan.FromSeconds(retryAfterSeconds)
            : DefaultSecondaryRetryDelay;

        return GitHubRateLimitException.CreateWithRetryAfter(
            retryAfter,
            "GitHub requested a temporary pause before retrying this scan.",
            abuseException);
    }

    private static GitHubRateLimitException CreateSecondaryRateLimitException(
        SecondaryRateLimitExceededException secondaryRateLimitExceededException)
    {
        var retryAfter = TryReadRetryAfterHeader(secondaryRateLimitExceededException.HttpResponse?.Headers)
            ?? DefaultSecondaryRetryDelay;

        return GitHubRateLimitException.CreateWithRetryAfter(
            retryAfter,
            "GitHub secondary rate limit exceeded. Waiting before retrying.",
            secondaryRateLimitExceededException);
    }

    private static TimeSpan? TryReadRetryAfterHeader(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (KeyValuePair<string, string> header in headers)
        {
            if (!string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(header.Value, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
        }

        return null;
    }
}
