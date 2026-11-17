using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.IntegrationTests.Fakes;

// Gönderilen bildirimleri kaydeder. Testler belirli alarmlarda hata fırlatmasını
// veya gönderim sırasında bir şey yapmasını (ör. alarmı silmek) isteyebilir.
public sealed class FakeAlertNotificationSender : IAlertNotificationSender
{
    public List<AlertNotification> Sent { get; } = [];

    public Func<AlertNotification, bool> ShouldFail { get; set; } = _ => false;

    public Func<AlertNotification, Task>? OnSend { get; set; }

    public async Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        if (ShouldFail(notification))
        {
            throw new InvalidOperationException($"Sahte gönderim hatası (alarm {notification.AlertId}).");
        }

        if (OnSend is not null)
        {
            await OnSend(notification);
        }

        Sent.Add(notification);
    }
}
