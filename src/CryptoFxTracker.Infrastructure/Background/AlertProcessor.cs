using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CryptoFxTracker.Infrastructure.Background;

// Bir fiyat partisi için aktif alarmları kontrol eder, tetiklenenlere bildirim gönderir ve kaydeder.
// Scoped: DbContext ile aynı ömürde. AlertCheckerService her parti için yeni scope açıp bunu çağırır;
// testler de doğrudan çağırabilir (zamanlamaya bağlı bekleme gerekmez).
public sealed class AlertProcessor(
    CryptoFxTrackerDbContext dbContext,
    IAlertNotificationSender sender,
    ILogger<AlertProcessor> logger)
{
    // Dönen değer: bildirimi gönderilip pasif yapılan alarm sayısı.
    public async Task<int> ProcessAsync(IReadOnlyCollection<PriceQuote> prices, CancellationToken cancellationToken)
    {
        var latestBySymbol = prices
            .GroupBy(price => price.Symbol)
            .ToDictionary(group => group.Key, group => group.Last());
        var symbols = latestBySymbol.Keys.ToList();

        // Koşul karşılaştırması bilerek SQL'de değil, entity'deki tek kuralda (IsTriggeredBy) yapılıyor.
        // Takas: o sembolün bütün aktif alarmları belleğe okunur; alarm sayısı çok büyürse SQL'e taşınmalı.
        var candidates = await dbContext.PriceAlerts
            .Include(alert => alert.User)
            .Where(alert => alert.IsActive && symbols.Contains(alert.Symbol))
            .ToListAsync(cancellationToken);

        var triggered = candidates
            .Where(alert => alert.IsTriggeredBy(latestBySymbol[alert.Symbol].Try))
            .ToList();
        if (triggered.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("{Count} alarm tetiklendi.", triggered.Count);

        var completed = 0;
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
                    PriceRetrievedAtUtc: price.RetrievedAtUtc,
                    TelegramChatId: alert.User.TelegramChatId), cancellationToken);
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
                completed++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Kullanıcı alarmı bu arada silmiş; güncellenecek satır kalmadı.
                dbContext.Entry(alert).State = EntityState.Detached;
                logger.LogInformation("Alarm {AlertId} bildirimden sonra silinmiş; durum güncellenmedi.", alert.Id);
            }
        }

        return completed;
    }
}
