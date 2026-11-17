namespace CryptoFxTracker.Infrastructure.Notifications;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    // Yalnızca ortam değişkeninden (Telegram__BotToken) gelmeli; koda veya appsettings'e yazılmaz.
    public string? BotToken { get; set; }

    // Kullanıcının kendi TelegramChatId'si yoksa kullanılan varsayılan sohbet.
    public long? ChatId { get; set; }
}
