namespace WhisperAPI;

public class RateLimitOptions
{
    public const string RateLimit = "RateLimit";

    public TimeSpan ReplenishmentPeriod = TimeSpan.FromSeconds(10);

    public int QueueLimit => 2;

    public int TokenLimit => Environment.ProcessorCount * 2;

    public int TokensPerPeriod => 2;

    public bool AutoReplenishment => false;
}