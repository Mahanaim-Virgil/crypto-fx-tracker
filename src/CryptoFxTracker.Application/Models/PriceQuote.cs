namespace CryptoFxTracker.Application.Models;

public sealed record PriceQuote(
    string Symbol,
    decimal Usd,
    decimal Try,
    DateTimeOffset RetrievedAtUtc,
    string Provider);
