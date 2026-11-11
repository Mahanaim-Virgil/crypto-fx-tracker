namespace CryptoFxTracker.Domain.Entities;

// Tek seferlik fiyat alarmı: koşul ilk sağlandığında bildirim gönderilir ve alarm pasif olur.
public sealed class PriceAlert
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Symbol { get; set; }
    public AlertCondition Condition { get; set; }
    public decimal TargetPriceTry { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? TriggeredAtUtc { get; set; }
    public decimal? TriggeredPriceTry { get; set; }

    // Hedefe tam ulaşmak da tetikler: "fiyat X'e gelince haber ver" beklentisine uyar.
    public bool IsTriggeredBy(decimal currentPriceTry) => IsActive && Condition switch
    {
        AlertCondition.Above => currentPriceTry >= TargetPriceTry,
        AlertCondition.Below => currentPriceTry <= TargetPriceTry,
        _ => false
    };

    public void MarkTriggered(decimal priceTry, DateTime triggeredAtUtc)
    {
        IsActive = false;
        TriggeredPriceTry = priceTry;
        TriggeredAtUtc = triggeredAtUtc;
    }
}
