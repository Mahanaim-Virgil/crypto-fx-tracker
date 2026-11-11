using System.Security.Claims;
using CryptoFxTracker.Api.Extensions;
using CryptoFxTracker.Application.Constants;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Domain.Entities;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.Api.Endpoints;

public static class AlertEndpoints
{
    // TargetPriceTry kolonu decimal(20,8): virgülden önce en fazla 12 basamak sığar.
    // Daha büyük bir değer 400 yerine SQL hatası (500) üretirdi.
    private const decimal MaxTargetPriceTry = 999_999_999_999m;

    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alerts")
            .RequireAuthorization()
            .WithTags("Alerts");

        group.MapPost("/", CreateAsync)
            .WithName("CreatePriceAlert")
            .Produces<PriceAlertResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/", ListAsync)
            .WithName("GetPriceAlerts")
            .Produces<IEnumerable<PriceAlertResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:long}", DeleteAsync)
            .WithName("DeletePriceAlert")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreatePriceAlertRequest request,
        ClaimsPrincipal user,
        CryptoFxTrackerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var symbol = request.Symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!PriceSymbols.Supported.Contains(symbol))
        {
            return Results.BadRequest(new { message = $"Desteklenen semboller: {string.Join(", ", PriceSymbols.Supported)}." });
        }

        if (!TryParseCondition(request.Condition, out var condition))
        {
            return Results.BadRequest(new { message = "Koşul 'Above' veya 'Below' olmalıdır." });
        }

        if (request.TargetPriceTry <= 0 || request.TargetPriceTry > MaxTargetPriceTry)
        {
            return Results.BadRequest(new { message = $"Hedef fiyat 0'dan büyük ve en fazla {MaxTargetPriceTry:N0} olmalıdır." });
        }

        // Kolon en fazla 8 ondalık basamak tutar; fazlası SQL'de sessizce yuvarlanırdı
        // (ör. 0.000000001 -> 0). Yuvarlamak yerine reddediyoruz ki kaydedilen değer gönderilenle aynı olsun.
        if (decimal.Round(request.TargetPriceTry, 8) != request.TargetPriceTry)
        {
            return Results.BadRequest(new { message = "Hedef fiyat en fazla 8 ondalık basamak içerebilir." });
        }

        var alert = new PriceAlert
        {
            UserId = user.GetUserId(),
            Symbol = symbol,
            Condition = condition,
            TargetPriceTry = request.TargetPriceTry,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.PriceAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Tek alarmı getiren bir GET endpoint'i olmadığı için Location başlığı verilmiyor.
        return Results.Json(ToResponse(alert), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        CryptoFxTrackerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var alerts = await dbContext.PriceAlerts
            .AsNoTracking()
            .Where(alert => alert.UserId == userId)
            .OrderByDescending(alert => alert.CreatedAtUtc)
            .ThenByDescending(alert => alert.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(alerts.Select(ToResponse));
    }

    private static async Task<IResult> DeleteAsync(
        long id,
        ClaimsPrincipal user,
        CryptoFxTrackerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        // Başka kullanıcının alarmı da 404 döner; 403 dönmek o id'nin var olduğunu sızdırırdı.
        var alert = await dbContext.PriceAlerts.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId,
            cancellationToken);
        if (alert is null)
        {
            return Results.NotFound();
        }

        dbContext.PriceAlerts.Remove(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // Enum.TryParse "1" veya "99" gibi sayıları da kabul ettiği için yalnızca isimler eşleştiriliyor.
    private static bool TryParseCondition(string? value, out AlertCondition condition)
    {
        condition = value?.Trim().ToLowerInvariant() switch
        {
            "above" => AlertCondition.Above,
            "below" => AlertCondition.Below,
            _ => default
        };
        return condition != default;
    }

    // EF Core datetime2 okurken DateTimeKind.Unspecified döndürür; JSON'da "Z" eksik kalır ve
    // istemci saati yerel saat sanabilir. Değerler UTC olarak yazıldığı için türü UTC olarak işaretliyoruz.
    private static PriceAlertResponse ToResponse(PriceAlert alert) => new(
        alert.Id,
        alert.Symbol,
        alert.Condition.ToString(),
        alert.TargetPriceTry,
        alert.IsActive,
        AsUtc(alert.CreatedAtUtc),
        alert.TriggeredAtUtc is { } triggeredAt ? AsUtc(triggeredAt) : null,
        alert.TriggeredPriceTry);

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
