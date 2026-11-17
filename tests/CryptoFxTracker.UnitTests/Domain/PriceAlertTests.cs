using CryptoFxTracker.Domain.Entities;

namespace CryptoFxTracker.UnitTests.Domain;

public sealed class PriceAlertTests
{
    private static PriceAlert CreateAlert(AlertCondition condition, decimal targetPriceTry) => new()
    {
        Symbol = "BTC",
        Condition = condition,
        TargetPriceTry = targetPriceTry,
        IsActive = true
    };

    [Theory]
    [InlineData("100", true)]          // hedefe tam ulaşmak tetikler
    [InlineData("100.00000001", true)]
    [InlineData("99.99999999", false)]
    public void Above_TriggersWhenPriceIsAtOrAboveTarget(string currentPrice, bool expected)
    {
        var alert = CreateAlert(AlertCondition.Above, 100m);

        Assert.Equal(expected, alert.IsTriggeredBy(decimal.Parse(currentPrice, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("100", true)]          // hedefe tam ulaşmak tetikler
    [InlineData("99.99999999", true)]
    [InlineData("100.00000001", false)]
    public void Below_TriggersWhenPriceIsAtOrBelowTarget(string currentPrice, bool expected)
    {
        var alert = CreateAlert(AlertCondition.Below, 100m);

        Assert.Equal(expected, alert.IsTriggeredBy(decimal.Parse(currentPrice, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void InactiveAlert_NeverTriggers()
    {
        var alert = CreateAlert(AlertCondition.Above, 100m);
        alert.IsActive = false;

        Assert.False(alert.IsTriggeredBy(1_000_000m));
    }

    [Fact]
    public void UndefinedCondition_NeverTriggers()
    {
        // Veritabanında beklenmeyen bir değer olsa bile alarm "her zaman çalar" duruma düşmemeli.
        var alert = CreateAlert((AlertCondition)0, 100m);

        Assert.False(alert.IsTriggeredBy(100m));
    }

    [Fact]
    public void MarkTriggered_DeactivatesAndRecordsPriceAndTime()
    {
        var alert = CreateAlert(AlertCondition.Above, 100m);
        var triggeredAt = new DateTime(2026, 11, 17, 8, 9, 22, DateTimeKind.Utc);

        alert.MarkTriggered(105.5m, triggeredAt);

        Assert.False(alert.IsActive);
        Assert.Equal(105.5m, alert.TriggeredPriceTry);
        Assert.Equal(triggeredAt, alert.TriggeredAtUtc);
    }

    [Fact]
    public void MarkedAlert_DoesNotTriggerAgain()
    {
        // Tek seferlik alarm: tetiklendikten sonra koşul hâlâ sağlansa da tekrar çalmamalı.
        var alert = CreateAlert(AlertCondition.Above, 100m);
        alert.MarkTriggered(105m, DateTime.UtcNow);

        Assert.False(alert.IsTriggeredBy(200m));
    }
}
