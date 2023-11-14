namespace WhisperAPI;

/// <summary>
/// Represents the options for rate limiting.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// The name of the rate limit policy.
    /// </summary>
    public const string RateLimit = "RateLimit";

    public TimeSpan ReplenishmentPeriod = TimeSpan.FromSeconds(10);

    public static int QueueLimit => 2;

    public static int TokenLimit => Environment.ProcessorCount * 2;

    public static int TokensPerPeriod => 2;

    public static bool AutoReplenishment => false;
}