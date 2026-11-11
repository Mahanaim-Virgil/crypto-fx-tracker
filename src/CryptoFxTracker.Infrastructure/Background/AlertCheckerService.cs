using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CryptoFxTracker.Infrastructure.Background;

// Fiyat toplayıcının kuyruğa koyduğu her fiyat partisinde aktif alarmları kontrol eder.
// Toplayıcıdan ayrı çalışır: bildirim yavaşlarsa veya hata verirse fiyat toplama etkilenmez.
public sealed class AlertCheckerService(
    PriceUpdateChannel priceUpdates,
    IServiceScopeFactory scopeFactory,
    ILogger<AlertCheckerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Alarm kontrol servisi başladı.");

        try
        {
            await foreach (var prices in priceUpdates.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await CheckAlertsAsync(prices, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Alarm kontrolünde beklenmeyen hata; sonraki fiyat partisinde devam edilecek.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        logger.LogInformation("Alarm kontrol servisi durdu.");
    }

    private async Task CheckAlertsAsync(IReadOnlyCollection<PriceQuote> prices, CancellationToken cancellationToken)
    {
        var latestBySymbol = prices
            .GroupBy(price => price.Symbol)
            .ToDictionary(group => group.Key, group => group.Last());
        var symbols = latestBySymbol.Keys.ToList();

        // BackgroundService singleton, DbContext scoped: her partide yeni scope açılır.
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CryptoFxTrackerDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IAlertNotificationSender>();

        // Koşul karşılaştırması bilerek SQL'de değil, entity'deki tek kuralda (IsTriggeredBy) yapılıyor.
        // Takas: o sembolün bütün aktif alarmları belleğe okunur; alarm sayısı çok büyürse SQL'e taşınmalı.
        var candidates = await dbContext.PriceAlerts
            .Where(alert => alert.IsActive && symbols.Contains(alert.Symbol))
            .ToListAsync(cancellationToken);

        var triggered = candidates
            .Where(alert => alert.IsTriggeredBy(latestBySymbol[alert.Symbol].Try))
            .ToList();
        if (triggered.Count == 0)
        {
            return;
        }

        logger.LogInformation("{Count} alarm tetiklendi.", triggered.Count);

        foreach (var alert in triggered)
        {
            var price = latestBySymbol[alert.Symbol];
            try
            {
                await sender.SendAsync(new AlertNotification(
                    AlertId: alert.Id,
                    UserId: alert.UserId,
                    Symbol: alert.Symbol,
                    Condition: alert.Condition.ToString(),
                    TargetPriceTry: alert.TargetPriceTry,
                    CurrentPriceTry: price.Try,
                    PriceRetrievedAtUtc: price.RetrievedAtUtc), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Alarm {AlertId} bildirimi gönderilemedi; alarm aktif kalıyor, sonraki fiyatta tekrar denenecek.", alert.Id);
                continue;
            }

            // Alarm, bildirim gittikten sonra ve her biri ayrı kaydedilerek pasif yapılır.
            // Kayıt başarısız olursa bildirim tekrar gidebilir (en az bir kez); kaybolması daha kötü olurdu.
            alert.MarkTriggered(price.Try, DateTime.UtcNow);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Kullanıcı alarmı bu arada silmiş; güncellenecek satır kalmadı.
                dbContext.Entry(alert).State = EntityState.Detached;
                logger.LogInformation("Alarm {AlertId} bildirimden sonra silinmiş; durum güncellenmedi.", alert.Id);
            }
        }
    }
}
