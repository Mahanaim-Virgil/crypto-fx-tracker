namespace CryptoFxTracker.Infrastructure.Background;

public sealed class PriceUpdateOptions
{
    public const string SectionName = "PriceUpdate";

    public int IntervalSeconds { get; set; } = 60;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
}
