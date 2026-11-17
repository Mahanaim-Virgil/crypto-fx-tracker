namespace CryptoFxTracker.Domain.Entities;

public sealed class User
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Alarm bildirimlerinin gideceği Telegram sohbeti. Boşsa varsayılan Telegram:ChatId kullanılır.
    public long? TelegramChatId { get; set; }
    public ICollection<UserWatchlist> Watchlist { get; set; } = [];
    public ICollection<PriceAlert> PriceAlerts { get; set; } = [];
}
