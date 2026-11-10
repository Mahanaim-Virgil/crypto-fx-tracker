namespace CryptoFxTracker.Domain.Entities;

public sealed class UserWatchlist
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Symbol { get; set; }
    public DateTime AddedAtUtc { get; set; }
}
