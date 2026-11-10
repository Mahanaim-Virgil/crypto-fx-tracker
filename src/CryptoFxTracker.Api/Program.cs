using CryptoFxTracker.Application.Constants;
using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure;
using CryptoFxTracker.Infrastructure.Auth;
using CryptoFxTracker.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtConfiguration = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSecret = jwtConfiguration["Secret"]
    ?? throw new InvalidOperationException("JWT secret yapılandırılmamış. Jwt:Secret veya Jwt__Secret sağlayın.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtConfiguration["Issuer"] ?? "CryptoFxTracker",
            ValidateAudience = true,
            ValidAudience = jwtConfiguration["Audience"] ?? "CryptoFxTracker.Client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", async (RegisterRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    try
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return response is null ? Results.Conflict(new { message = "Bu e-posta zaten kayıtlı." }) : Results.Ok(response);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
})
.WithName("Register")
.WithTags("Auth");

app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    var response = await authService.LoginAsync(request, cancellationToken);
    return response is null ? Results.Unauthorized() : Results.Ok(response);
})
.WithName("Login")
.WithTags("Auth");

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

app.MapGet("/api/watchlist", async (ClaimsPrincipal user, CryptoFxTrackerDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = GetUserId(user);
    var symbols = await dbContext.UserWatchlists
        .Where(item => item.UserId == userId)
        .OrderBy(item => item.Symbol)
        .Select(item => item.Symbol)
        .ToListAsync(cancellationToken);
    return Results.Ok(symbols);
})
.RequireAuthorization()
.WithName("GetWatchlist")
.WithTags("Watchlist");

app.MapPost("/api/watchlist/{symbol}", async (string symbol, ClaimsPrincipal user, CryptoFxTrackerDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = GetUserId(user);
    var normalizedSymbol = symbol.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(normalizedSymbol) || normalizedSymbol.Length > 20)
    {
        return Results.BadRequest(new { message = "Geçerli bir sembol gönderilmelidir." });
    }

    var exists = await dbContext.UserWatchlists.AnyAsync(
        item => item.UserId == userId && item.Symbol == normalizedSymbol,
        cancellationToken);
    if (!exists)
    {
        dbContext.UserWatchlists.Add(new CryptoFxTracker.Domain.Entities.UserWatchlist
        {
            UserId = userId,
            Symbol = normalizedSymbol,
            AddedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new { symbol = normalizedSymbol });
})
.RequireAuthorization()
.WithName("AddToWatchlist")
.WithTags("Watchlist");

app.MapDelete("/api/watchlist/{symbol}", async (string symbol, ClaimsPrincipal user, CryptoFxTrackerDbContext dbContext, CancellationToken cancellationToken) =>
{
    var userId = GetUserId(user);
    var normalizedSymbol = symbol.Trim().ToUpperInvariant();
    var item = await dbContext.UserWatchlists.FindAsync([userId, normalizedSymbol], cancellationToken);
    if (item is null)
    {
        return Results.NotFound();
    }

    dbContext.UserWatchlists.Remove(item);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
.RequireAuthorization()
.WithName("RemoveFromWatchlist")
.WithTags("Watchlist");

app.Run();

static long GetUserId(ClaimsPrincipal user)
{
    var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return long.TryParse(value, out var userId)
        ? userId
        : throw new UnauthorizedAccessException("JWT kullanıcı kimliği bulunamadı.");
}

public partial class Program;
