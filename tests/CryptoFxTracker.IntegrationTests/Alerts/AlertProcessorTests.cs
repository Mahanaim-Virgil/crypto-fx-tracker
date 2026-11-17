using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Domain.Entities;
using CryptoFxTracker.Infrastructure.Background;
using CryptoFxTracker.IntegrationTests.Fakes;
using CryptoFxTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoFxTracker.IntegrationTests.Alerts;

public sealed class AlertProcessorTests : IDisposable
{
    private readonly SqliteDatabase database = new();
    private readonly FakeAlertNotificationSender sender = new();

    public void Dispose() => database.Dispose();

    private long SeedUser(long? telegramChatId = null)
    {
        using var context = database.CreateContext();
        var user = new User
        {
            Email = $"user-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            CreatedAtUtc = DateTime.UtcNow,
            TelegramChatId = telegramChatId
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user.Id;
    }

    private long SeedAlert(long userId, string symbol, AlertCondition condition, decimal targetPriceTry)
    {
        using var context = database.CreateContext();
        var alert = new PriceAlert
        {
            UserId = userId,
            Symbol = symbol,
            Condition = condition,
            TargetPriceTry = targetPriceTry,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.PriceAlerts.Add(alert);
        context.SaveChanges();
        return alert.Id;
    }

    private static IReadOnlyCollection<PriceQuote> Prices(params (string Symbol, decimal Try)[] quotes) =>
        quotes.Select(quote => new PriceQuote(quote.Symbol, 1m, quote.Try, DateTimeOffset.UtcNow, "Test")).ToList();

    // Her çağrı ayrı bir context kullanır; uygulamadaki "her partide yeni scope" davranışıyla aynı.
    private async Task<int> ProcessAsync(IReadOnlyCollection<PriceQuote> prices)
    {
        using var context = database.CreateContext();
        var processor = new AlertProcessor(context, sender, NullLogger<AlertProcessor>.Instance);
        return await processor.ProcessAsync(prices, CancellationToken.None);
    }

    private PriceAlert? FindAlert(long id)
    {
        using var context = database.CreateContext();
        return context.PriceAlerts.AsNoTracking().SingleOrDefault(alert => alert.Id == id);
    }

    [Fact]
    public async Task TriggeredAlert_IsNotifiedOnce_AndMarkedInactive_OthersStayActive()
    {
        var userId = SeedUser(telegramChatId: 555);
        var fires = SeedAlert(userId, "BTC", AlertCondition.Above, 100m);
        var tooHigh = SeedAlert(userId, "BTC", AlertCondition.Above, 1_000_000_000m);
        var otherSymbol = SeedAlert(userId, "USDTRY", AlertCondition.Below, 40m);

        var completed = await ProcessAsync(Prices(("BTC", 200m), ("USDTRY", 50m)));

        Assert.Equal(1, completed);
        var notification = Assert.Single(sender.Sent);
        Assert.Equal(fires, notification.AlertId);
        Assert.Equal(200m, notification.CurrentPriceTry);
        Assert.Equal(555, notification.TelegramChatId);

        var fired = FindAlert(fires)!;
        Assert.False(fired.IsActive);
        Assert.Equal(200m, fired.TriggeredPriceTry);
        Assert.NotNull(fired.TriggeredAtUtc);
        Assert.True(FindAlert(tooHigh)!.IsActive);
        Assert.True(FindAlert(otherSymbol)!.IsActive);
    }

    [Fact]
    public async Task FiredAlert_IsNotNotifiedAgain_OnNextBatch()
    {
        var userId = SeedUser();
        SeedAlert(userId, "BTC", AlertCondition.Above, 100m);

        await ProcessAsync(Prices(("BTC", 200m)));
        var secondRun = await ProcessAsync(Prices(("BTC", 300m)));

        Assert.Equal(0, secondRun);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task SendFailure_KeepsAlertActive_OtherAlertsContinue_AndRetryLaterSucceeds()
    {
        var userId = SeedUser();
        var failing = SeedAlert(userId, "BTC", AlertCondition.Above, 100m);
        var healthy = SeedAlert(userId, "BTC", AlertCondition.Above, 150m);
        sender.ShouldFail = notification => notification.AlertId == failing;

        var completed = await ProcessAsync(Prices(("BTC", 200m)));

        Assert.Equal(1, completed);
        Assert.True(FindAlert(failing)!.IsActive);
        Assert.False(FindAlert(healthy)!.IsActive);

        // Gönderim düzelince bir sonraki partide alarm çalar.
        sender.ShouldFail = _ => false;
        var retry = await ProcessAsync(Prices(("BTC", 200m)));

        Assert.Equal(1, retry);
        Assert.False(FindAlert(failing)!.IsActive);
    }

    [Fact]
    public async Task AlertDeletedWhileNotifying_DoesNotBreakTheBatch()
    {
        // Senaryo: bildirim gittikten hemen sonra kullanıcı API'den alarmı siliyor.
        // Kayıt sırasında UPDATE 0 satırı etkiler -> DbUpdateConcurrencyException.
        // Bu hata partiyi bozmamalı, diğer alarm yine kaydedilmeli.
        var userId = SeedUser();
        var deleted = SeedAlert(userId, "BTC", AlertCondition.Above, 100m);
        var other = SeedAlert(userId, "BTC", AlertCondition.Above, 150m);
        sender.OnSend = async notification =>
        {
            if (notification.AlertId != deleted)
            {
                return;
            }

            // Ayrı context = API'nin ayrı bir isteği.
            await using var apiContext = database.CreateContext();
            await apiContext.PriceAlerts.Where(alert => alert.Id == deleted).ExecuteDeleteAsync();
        };

        var completed = await ProcessAsync(Prices(("BTC", 200m)));

        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal(1, completed);
        Assert.Null(FindAlert(deleted));
        Assert.False(FindAlert(other)!.IsActive);
    }

    [Fact]
    public async Task SymbolMissingFromBatch_IsNotEvaluated()
    {
        // TCMB başarısız olduğu bir döngüde partide yalnızca BTC olabilir.
        var userId = SeedUser();
        var usdAlert = SeedAlert(userId, "USDTRY", AlertCondition.Below, 100m);

        var completed = await ProcessAsync(Prices(("BTC", 200m)));

        Assert.Equal(0, completed);
        Assert.Empty(sender.Sent);
        Assert.True(FindAlert(usdAlert)!.IsActive);
    }
}
