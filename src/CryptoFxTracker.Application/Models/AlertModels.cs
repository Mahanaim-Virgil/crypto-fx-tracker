namespace CryptoFxTracker.Application.Models;

public sealed record CreatePriceAlertRequest(string Symbol, string Condition, decimal TargetPriceTry);

public sealed record AlertNotification(
    long AlertId,
    long UserId,
    string Symbol,
    string Condition,
    decimal TargetPriceTry,
    decimal CurrentPriceTry,
    DateTimeOffset PriceRetrievedAtUtc);

public sealed record PriceAlertResponse(
    long Id,
    string Symbol,
    string Condition,
    decimal TargetPriceTry,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? TriggeredAtUtc,
    decimal? TriggeredPriceTry);
