using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using Microsoft.Extensions.Logging;

namespace CryptoFxTracker.Infrastructure.Notifications;

// Bildirimi yalnızca loga yazar. Telegram yapılandırılmadığında da alarm akışı
// uçtan uca çalışıp gözlemlenebilsin diye var.
public sealed class LoggingAlertNotificationSender(ILogger<LoggingAlertNotificationSender> logger) : IAlertNotificationSender
{
    public Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "ALARM {AlertId} (kullanıcı {UserId}): {Symbol} {Condition} {TargetPriceTry} TRY koşulu sağlandı, güncel fiyat {CurrentPriceTry} TRY.",
            notification.AlertId,
            notification.UserId,
            notification.Symbol,
            notification.Condition,
            notification.TargetPriceTry,
            notification.CurrentPriceTry);
        return Task.CompletedTask;
    }
}
