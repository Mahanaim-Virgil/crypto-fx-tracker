namespace CryptoFxTracker.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Secret { get; set; }
    public string Issuer { get; set; } = "CryptoFxTracker";
    public string Audience { get; set; } = "CryptoFxTracker.Client";
    public int ExpirationMinutes { get; set; } = 60;
}
