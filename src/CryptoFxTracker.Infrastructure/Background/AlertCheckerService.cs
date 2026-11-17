using CryptoFxTracker.Application.Abstractions;
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
        using (var scope = scopeFactory.CreateScope())
        {
            var senderName = scope.ServiceProvider.GetRequiredService<IAlertNotificationSender>().GetType().Name;
            logger.LogInformation("Alarm kontrol servisi başladı. Bildirim gönderici: {Sender}", senderName);
        }

        try
        {
            await foreach (var prices in priceUpdates.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // BackgroundService singleton, DbContext scoped: her partide yeni scope açılır.
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AlertProcessor>().ProcessAsync(prices, stoppingToken);
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
}
