namespace CryptoFxTracker.Application.Models;

public sealed record CreatePriceAlertRequest(string Symbol, string Condition, decimal TargetPriceTry);

public sealed record PriceAlertResponse(
    long Id,
    string Symbol,
    string Condition,
    decimal TargetPriceTry,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? TriggeredAtUtc,
    decimal? TriggeredPriceTry);
