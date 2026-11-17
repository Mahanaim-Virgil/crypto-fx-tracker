using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CryptoFxTracker.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.IntegrationTests.Api;

public sealed class AlertEndpointsTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    // Ham JSON gönderilir: sayılar (ör. 9 ondalık) ve koşul metni tam olarak istemcinin yazdığı gibi gitsin.
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":1}"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidAlert_Returns201_WithNormalizedSymbolAndUtcDate()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/alerts", Json("""{"symbol":" usdtry ","condition":"below","targetPriceTry":40.5}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("USDTRY", body.GetProperty("symbol").GetString());
        Assert.Equal("Below", body.GetProperty("condition").GetString());
        Assert.Equal(40.5m, body.GetProperty("targetPriceTry").GetDecimal());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.EndsWith("Z", body.GetProperty("createdAtUtc").GetString());
    }

    [Theory]
    [InlineData("""{"symbol":"XYZ","condition":"Above","targetPriceTry":1}""", "Desteklenen semboller")]
    [InlineData("""{"symbol":"BTC","condition":"1","targetPriceTry":1}""", "Koşul")]
    [InlineData("""{"symbol":"BTC","condition":"Sideways","targetPriceTry":1}""", "Koşul")]
    [InlineData("""{"symbol":"BTC","condition":null,"targetPriceTry":1}""", "Koşul")]
    [InlineData("""{"symbol":"BTC","condition":"Above","targetPriceTry":0}""", "0'dan büyük")]
    [InlineData("""{"symbol":"BTC","condition":"Above","targetPriceTry":-5}""", "0'dan büyük")]
    [InlineData("""{"symbol":"BTC","condition":"Above","targetPriceTry":10000000000000}""", "0'dan büyük")]
    [InlineData("""{"symbol":"BTC","condition":"Above","targetPriceTry":0.000000001}""", "8 ondalık")]
    [InlineData("""{}""", "Desteklenen semboller")]
    public async Task Create_InvalidInput_Returns400_WithMessage(string json, string expectedMessagePart)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/alerts", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessagePart, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_InvalidInput_DoesNotWriteToDatabase()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        await client.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":0.000000001}"""));

        Assert.Equal(0, await factory.QueryAsync(db => db.PriceAlerts.CountAsync()));
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnAlerts()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var stranger = await factory.CreateAuthenticatedClientAsync();
        await owner.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":1}"""));

        var ownerAlerts = await owner.GetFromJsonAsync<List<PriceAlertResponse>>("/api/alerts");
        var strangerAlerts = await stranger.GetFromJsonAsync<List<PriceAlertResponse>>("/api/alerts");

        Assert.Single(ownerAlerts!);
        Assert.Empty(strangerAlerts!);
    }

    [Fact]
    public async Task List_ReturnsDatesMarkedAsUtc()
    {
        // Regresyon testi: veritabanından okunan tarih "Z"siz dönüyordu.
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":1}"""));

        var json = JsonDocument.Parse(await client.GetStringAsync("/api/alerts")).RootElement;

        Assert.EndsWith("Z", json[0].GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    public async Task Delete_OtherUsersAlert_Returns404_AndKeepsIt()
    {
        // 403 değil 404: başka kullanıcının alarm id'sinin var olduğu sızdırılmaz.
        var owner = await factory.CreateAuthenticatedClientAsync();
        var stranger = await factory.CreateAuthenticatedClientAsync();
        var created = await (await owner.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":1}""")))
            .Content.ReadFromJsonAsync<PriceAlertResponse>();

        var response = await stranger.DeleteAsync($"/api/alerts/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, await factory.QueryAsync(db => db.PriceAlerts.CountAsync()));
    }

    [Fact]
    public async Task Delete_OwnAlert_Returns204_ThenSecondDeleteReturns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await (await client.PostAsync("/api/alerts", Json("""{"symbol":"BTC","condition":"Above","targetPriceTry":1}""")))
            .Content.ReadFromJsonAsync<PriceAlertResponse>();

        var first = await client.DeleteAsync($"/api/alerts/{created!.Id}");
        var second = await client.DeleteAsync($"/api/alerts/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }
}
