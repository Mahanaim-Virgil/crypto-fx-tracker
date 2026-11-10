namespace CryptoFxTracker.Domain.Entities;

public sealed class User
{
    public long Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<UserWatchlist> Watchlist { get; set; } = [];
}
