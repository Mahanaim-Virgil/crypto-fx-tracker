using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Infrastructure;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/prices", async (IPriceProvider priceProvider, CancellationToken cancellationToken) =>
{
    try
    {
        var price = await priceProvider.GetBitcoinPriceAsync(cancellationToken);
        return Results.Ok(price);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(
            title: "Fiyat sağlayıcısı hatası",
            detail: exception.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("GetBitcoinPrice")
.WithTags("Prices")
.Produces(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status502BadGateway);

app.MapGet("/api/price-history", async (CryptoFxTrackerDbContext dbContext, CancellationToken cancellationToken) =>
    Results.Ok(await dbContext.PriceHistories
        .OrderByDescending(price => price.RecordedAtUtc)
        .Take(100)
        .ToListAsync(cancellationToken)))
    .WithName("GetPriceHistory")
    .WithTags("Prices");

app.Run();

public partial class Program;
