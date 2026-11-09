namespace CryptoFxTracker.Domain.Entities;

public sealed class PriceHistory
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public required string Provider { get; set; }
    public decimal PriceUsd { get; set; }
    public decimal PriceTry { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
