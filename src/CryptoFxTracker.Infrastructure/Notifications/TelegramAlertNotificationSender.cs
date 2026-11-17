using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using Microsoft.Extensions.Options;

namespace CryptoFxTracker.Infrastructure.Notifications;

// Telegram Bot API (sendMessage) ile alarm bildirimi gönderir.
// Bot token'ı istek adresinin içindedir; bu yüzden adres hiçbir log veya hata mesajına yazılmaz
// (HttpClient logları DependencyInjection'da bu istemci için kapatıldı).
public sealed class TelegramAlertNotificationSender(
    HttpClient httpClient,
    IOptions<TelegramOptions> options) : IAlertNotificationSender
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task SendAsync(AlertNotification notification, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var chatId = notification.TelegramChatId ?? settings.ChatId
            ?? throw new InvalidOperationException(
                $"Kullanıcı {notification.UserId} için Telegram ChatId yok ve varsayılan Telegram:ChatId ayarlanmamış.");

        // Baştaki "/" şart: token "123456:ABC..." biçiminde olduğu için "bot123456:ABC.../sendMessage"
        // .NET'te "bot123456:" şemalı MUTLAK adres sayılır ve BaseAddress yok sayılırdı.
        using var response = await httpClient.PostAsJsonAsync(
            $"/bot{settings.BotToken}/sendMessage",
            new { chat_id = chatId, text = BuildText(notification) },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var description = await ReadDescriptionAsync(response, cancellationToken);
            throw new InvalidOperationException(
                $"Telegram mesajı gönderilemedi (HTTP {(int)response.StatusCode}): {description}");
        }
    }

    private static string BuildText(AlertNotification notification)
    {
        var direction = notification.Condition == "Above" ? "üstüne çıktı" : "altına indi";
        return $"""
            🔔 Fiyat alarmı: {notification.Symbol}
            Fiyat {Format(notification.TargetPriceTry)} TRY {direction}.
            Güncel fiyat: {Format(notification.CurrentPriceTry)} TRY
            Alarm no: {notification.AlertId}
            """;
    }

    private static string Format(decimal value) => value.ToString("#,0.########", Turkish);

    // Telegram hata yanıtı: {"ok":false,"error_code":400,"description":"Bad Request: chat not found"}
    private static async Task<string> ReadDescriptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("description", out var description)
                ? description.GetString() ?? "açıklama yok"
                : "açıklama yok";
        }
        catch (JsonException)
        {
            return "yanıt JSON değil";
        }
    }
}
