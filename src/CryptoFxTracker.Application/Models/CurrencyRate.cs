namespace CryptoFxTracker.Application.Models;

public sealed record CurrencyRate(
    string CurrencyCode,
    decimal TryValue,
    DateTime RateDate,
    string Provider);
