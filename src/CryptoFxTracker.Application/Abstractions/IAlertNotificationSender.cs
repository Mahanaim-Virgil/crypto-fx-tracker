using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Application.Abstractions;

// Tetiklenen alarmı kullanıcıya iletir. Hata durumunda exception fırlatmalıdır;
// alarm servisi bu durumda alarmı aktif bırakır ve sonraki fiyatta tekrar dener.
public interface IAlertNotificationSender
{
    Task SendAsync(AlertNotification notification, CancellationToken cancellationToken);
}
