namespace EmojiEstimator.Web.Services;

public sealed class GitHubRateLimitException : Exception
{
    private GitHubRateLimitException(
        string message,
        DateTimeOffset? retryAtUtc,
        TimeSpan? retryAfter,
        Exception? innerException) : base(message, innerException)
    {
        RetryAtUtc = retryAtUtc;
        RetryAfter = retryAfter;
    }

    public DateTimeOffset? RetryAtUtc { get; }

    public TimeSpan? RetryAfter { get; }

    public static GitHubRateLimitException CreateWithRetryAt(
        DateTimeOffset retryAtUtc,
        string message,
        Exception? innerException = null) =>
        new(message, retryAtUtc, null, innerException);

    public static GitHubRateLimitException CreateWithRetryAfter(
        TimeSpan retryAfter,
        string message,
        Exception? innerException = null) =>
        new(message, null, retryAfter, innerException);

    public TimeSpan GetRetryDelay(DateTimeOffset utcNow)
    {
        if (RetryAtUtc is DateTimeOffset retryAtUtc)
        {
            var retryDelay = retryAtUtc - utcNow;
            return retryDelay > TimeSpan.Zero ? retryDelay : TimeSpan.Zero;
        }

        if (RetryAfter is TimeSpan retryAfter)
        {
            return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero;
        }

        return TimeSpan.Zero;
    }
}
