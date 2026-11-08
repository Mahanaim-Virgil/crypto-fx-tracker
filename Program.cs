using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Infrastructure.CoinGecko;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IPriceProvider, CoinGeckoProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoFxTracker/1.0");
});

var app = builder.Build();

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

app.MapGet("/swagger/v1/swagger.json", () => Results.Content("""
{
  "openapi": "3.0.3",
  "info": { "title": "Crypto & FX Tracker API", "version": "v1" },
  "paths": {
    "/api/prices": {
      "get": {
        "tags": ["Prices"],
        "summary": "BTC fiyatını USD ve TRY olarak getirir",
        "responses": {
          "200": { "description": "Başarılı fiyat cevabı" },
          "502": { "description": "CoinGecko erişim veya cevap hatası" }
        }
      }
    }
  }
}
""", "application/json"));

app.MapGet("/swagger", () => Results.Content("""
<!doctype html>
<html lang="tr">
<head><meta charset="utf-8"><title>Crypto & FX Tracker - Swagger</title>
<link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css"></head>
<body><div id="swagger-ui"></div>
<script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
<script>window.onload = () => SwaggerUIBundle({ url: '/swagger/v1/swagger.json', dom_id: '#swagger-ui' });</script>
</body></html>
""", "text/html"));

app.Run();

public partial class Program;
