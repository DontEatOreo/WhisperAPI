namespace WhisperAPI;

public class MyRateLimitOptions
{
    public const string MyRateLimit = "MyRateLimit";
    public static TimeSpan ReplenishmentPeriod => TimeSpan.MaxValue;
    public static int QueueLimit => 2;
    public static int TokenLimit => Environment.ProcessorCount * 2;
    public static int TokensPerPeriod => 2;
    public static bool AutoReplenishment => false;
}