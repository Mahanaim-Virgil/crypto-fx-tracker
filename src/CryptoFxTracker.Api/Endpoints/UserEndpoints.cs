using System.Security.Claims;
using CryptoFxTracker.Api.Extensions;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Persistence;

namespace CryptoFxTracker.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/me")
            .RequireAuthorization()
            .WithTags("Users");

        // Alarm bildirimlerinin gideceği Telegram sohbetini ayarlar; chatId null ise kaldırır.
        // Not: ChatId'nin gerçekten bu kullanıcıya ait olduğu doğrulanmıyor (bilinen sınırlama).
        group.MapPut("/telegram", SetTelegramChatIdAsync)
            .WithName("SetTelegramChatId")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> SetTelegramChatIdAsync(
        SetTelegramChatIdRequest request,
        ClaimsPrincipal user,
        CryptoFxTrackerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.ChatId == 0)
        {
            return Results.BadRequest(new { message = "Geçerli bir Telegram ChatId gönderilmelidir." });
        }

        var account = await dbContext.Users.FindAsync([user.GetUserId()], cancellationToken);
        if (account is null)
        {
            // Token geçerli ama kullanıcı silinmiş.
            return Results.Unauthorized();
        }

        account.TelegramChatId = request.ChatId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
