namespace EmojiEstimator.PlaywrightTests.Infrastructure;

public sealed class MutableTimeProvider : TimeProvider
{
    private readonly object syncRoot = new();
    private DateTimeOffset utcNow;

    public MutableTimeProvider(DateTimeOffset initialUtcNow)
    {
        utcNow = initialUtcNow;
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (syncRoot)
        {
            utcNow = value;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (syncRoot)
        {
            return utcNow;
        }
    }
}
