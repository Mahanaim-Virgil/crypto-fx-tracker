using CryptoFxTracker.Application.Constants;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/prices", (IMemoryCache memoryCache) =>
{
    if (memoryCache.TryGetValue(PriceCacheKeys.CurrentPrices, out IReadOnlyCollection<PriceQuote>? prices) && prices is not null)
    {
        return Results.Ok(prices);
    }
    return Results.Problem(
        title: "Güncel fiyat bulunamadı",
        detail: "BackgroundService henüz başarılı bir fiyat toplama döngüsü tamamlamadı.",
        statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("GetCurrentPrices")
.WithTags("Prices")
.Produces(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/price-history", async (CryptoFxTrackerDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.PriceHistories
        .OrderByDescending(price => price.RecordedAtUtc)
        .Take(100)
        .ToListAsync(cancellationToken)))
    .WithName("GetPriceHistory")
    .WithTags("Prices");

app.Run();

public partial class Program;
