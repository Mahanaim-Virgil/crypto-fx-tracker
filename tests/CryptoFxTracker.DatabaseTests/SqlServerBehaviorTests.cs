using CryptoFxTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.DatabaseTests;

// SQLite'ta görünmeyen, gerçek SQL Server'a özgü davranışlar.
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerBehaviorTests(SqlServerFixture fixture)
{
    private async Task<long> SeedUserAsync()
    {
        await using var context = fixture.CreateContext();
        var user = new User { Email = $"user-{Guid.NewGuid():N}@example.test", PasswordHash = "hash", CreatedAtUtc = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<decimal> StoreAndReadTargetPriceAsync(long userId, decimal targetPriceTry)
    {
        long id;
        await using (var context = fixture.CreateContext())
        {
            var alert = new PriceAlert
            {
                UserId = userId,
                Symbol = "BTC",
                Condition = AlertCondition.Above,
                TargetPriceTry = targetPriceTry,
                CreatedAtUtc = DateTime.UtcNow
            };
            context.PriceAlerts.Add(alert);
            await context.SaveChangesAsync();
            id = alert.Id;
        }

        await using var readContext = fixture.CreateContext();
        return await readContext.PriceAlerts.Where(alert => alert.Id == id).Select(alert => alert.TargetPriceTry).SingleAsync();
    }

    [Fact]
    public async Task DecimalColumn_SilentlyChangesValuesWithMoreThanEightDecimals()
    {
        // Bu test, /api/alerts'in 8'den fazla ondalığı NEDEN 400 ile reddettiğini kanıtlar:
        // decimal(20,8) kolonu fazlasını hata vermeden atar, 0.000000001 ise 0 olur.
        var userId = await SeedUserAsync();

        var exact = await StoreAndReadTargetPriceAsync(userId, 100.12345678m);
        var tooPrecise = await StoreAndReadTargetPriceAsync(userId, 100.123456789m);
        var tiny = await StoreAndReadTargetPriceAsync(userId, 0.000000001m);

        Assert.Equal(100.12345678m, exact);
        Assert.NotEqual(100.123456789m, tooPrecise);
        Assert.Contains(tooPrecise, new[] { 100.12345679m, 100.12345678m }); // yuvarlama veya kesme
        Assert.Equal(0m, tiny);
    }

    [Fact]
    public async Task PriceHistory_OrdersByRecordedAtAsInstants()
    {
        // /api/price-history'nin sorgusu (DateTimeOffset'e göre sıralama) SQLite'ta çalışmıyordu.
        // Farklı saat dilimleri: 10:00+03:00 aslında 07:00Z, yani 08:00Z'den önce.
        var symbol = $"T{Guid.NewGuid():N}"[..20];
        await using (var context = fixture.CreateContext())
        {
            context.PriceHistories.AddRange(
                new PriceHistory { Symbol = symbol, Provider = "Test", PriceTry = 1m, RecordedAtUtc = new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.FromHours(3)) },
                new PriceHistory { Symbol = symbol, Provider = "Test", PriceTry = 2m, RecordedAtUtc = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero) },
                new PriceHistory { Symbol = symbol, Provider = "Test", PriceTry = 3m, RecordedAtUtc = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero) });
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var newestFirst = await readContext.PriceHistories
            .Where(price => price.Symbol == symbol)
            .OrderByDescending(price => price.RecordedAtUtc)
            .Select(price => price.PriceTry)
            .ToListAsync();

        Assert.Equal(new[] { 3m, 2m, 1m }, newestFirst);
    }
}
