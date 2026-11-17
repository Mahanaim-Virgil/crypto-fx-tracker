using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.IntegrationTests.Api;

public sealed class UserEndpointsTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private Task<long?> StoredChatIdAsync() =>
        factory.QueryAsync(db => db.Users.Select(user => user.TelegramChatId).SingleAsync());

    [Fact]
    public async Task SetTelegramChatId_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PutAsync("/api/users/me/telegram", Json("""{"chatId":555}"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetTelegramChatId_Stores_ThenNullClears()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var set = await client.PutAsync("/api/users/me/telegram", Json("""{"chatId":555}"""));
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        Assert.Equal(555, await StoredChatIdAsync());

        var clear = await client.PutAsync("/api/users/me/telegram", Json("""{"chatId":null}"""));
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Null(await StoredChatIdAsync());
    }

    [Fact]
    public async Task SetTelegramChatId_Zero_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsync("/api/users/me/telegram", Json("""{"chatId":0}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await StoredChatIdAsync());
    }
}
