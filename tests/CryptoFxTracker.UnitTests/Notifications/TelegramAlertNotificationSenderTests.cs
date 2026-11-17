using System.Net;
using System.Text.Json;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Notifications;
using CryptoFxTracker.UnitTests.Fakes;
using Microsoft.Extensions.Options;

namespace CryptoFxTracker.UnitTests.Notifications;

public sealed class TelegramAlertNotificationSenderTests
{
    // Gerçek token biçimi: "<sayı>:<gizli kısım>". Hata bu ':' yüzünden çıkmıştı.
    private const string BotToken = "123456789:AAH-test_secret";

    private static (TelegramAlertNotificationSender Sender, RecordingHttpMessageHandler Handler) CreateSender(
        long? defaultChatId = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string responseBody = """{"ok":true}""")
    {
        var handler = new RecordingHttpMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = TelegramAlertNotificationSender.ApiBaseAddress };
        var options = Options.Create(new TelegramOptions { BotToken = BotToken, ChatId = defaultChatId });
        return (new TelegramAlertNotificationSender(httpClient, options), handler);
    }

    private static AlertNotification CreateNotification(
        long? telegramChatId = 555,
        string condition = "Above",
        decimal targetPriceTry = 3_700_000m,
        decimal currentPriceTry = 3_739_253m) => new(
            AlertId: 42,
            UserId: 7,
            Symbol: "BTC",
            Condition: condition,
            TargetPriceTry: targetPriceTry,
            CurrentPriceTry: currentPriceTry,
            PriceRetrievedAtUtc: DateTimeOffset.UtcNow,
            TelegramChatId: telegramChatId);

    private static JsonElement ReadBody(RecordedRequest request) => JsonDocument.Parse(request.Body!).RootElement;

    [Fact]
    public async Task PostsToSendMessageUrl_WithBotTokenInPath()
    {
        // Regresyon testi: "bot123456789:AAH.../sendMessage" .NET'te "bot123456789:" şemalı mutlak adres
        // sayılıyordu; BaseAddress yok sayılıp hiçbir mesaj gönderilemiyordu.
        var (sender, handler) = CreateSender();

        await sender.SendAsync(CreateNotification(), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"https://api.telegram.org/bot{BotToken}/sendMessage", request.Uri?.AbsoluteUri);
    }

    [Fact]
    public async Task UsesUsersOwnChatId_WhenPresent()
    {
        var (sender, handler) = CreateSender(defaultChatId: 999);

        await sender.SendAsync(CreateNotification(telegramChatId: 555), CancellationToken.None);

        Assert.Equal(555, ReadBody(Assert.Single(handler.Requests)).GetProperty("chat_id").GetInt64());
    }

    [Fact]
    public async Task FallsBackToDefaultChatId_WhenUserHasNone()
    {
        var (sender, handler) = CreateSender(defaultChatId: 999);

        await sender.SendAsync(CreateNotification(telegramChatId: null), CancellationToken.None);

        Assert.Equal(999, ReadBody(Assert.Single(handler.Requests)).GetProperty("chat_id").GetInt64());
    }

    [Fact]
    public async Task Throws_WithoutSendingRequest_WhenNoChatIdAnywhere()
    {
        var (sender, handler) = CreateSender(defaultChatId: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(CreateNotification(telegramChatId: null), CancellationToken.None));

        Assert.Contains("ChatId yok", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Throws_WithTelegramDescription_AndWithoutToken_WhenTelegramRejects()
    {
        var (sender, _) = CreateSender(
            statusCode: HttpStatusCode.BadRequest,
            responseBody: """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(CreateNotification(), CancellationToken.None));

        Assert.Contains("HTTP 400", exception.Message);
        Assert.Contains("chat not found", exception.Message);
        // Hata mesajı loga yazılır; token'ın gizli kısmı içinde olmamalı.
        Assert.DoesNotContain("AAH-test_secret", exception.ToString());
    }

    [Fact]
    public async Task Throws_OnRateLimit_SoTheAlertStaysActiveForRetry()
    {
        // 429 için ayrı bir davranış yok: diğer hatalar gibi exception fırlatılır,
        // AlertCheckerService alarmı aktif bırakır ve sonraki fiyat partisinde tekrar dener.
        var (sender, _) = CreateSender(
            statusCode: HttpStatusCode.TooManyRequests,
            responseBody: """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 5"}""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(CreateNotification(), CancellationToken.None));

        Assert.Contains("HTTP 429", exception.Message);
    }

    [Fact]
    public async Task Throws_WithReadableMessage_WhenErrorBodyIsNotJson()
    {
        var (sender, _) = CreateSender(statusCode: HttpStatusCode.BadGateway, responseBody: "<html>502</html>");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(CreateNotification(), CancellationToken.None));

        Assert.Contains("yanıt JSON değil", exception.Message);
    }

    [Theory]
    [InlineData("Above", "üstüne çıktı")]
    [InlineData("Below", "altına indi")]
    public async Task MessageText_DescribesConditionAndUsesTurkishNumberFormat(string condition, string expectedPhrase)
    {
        var (sender, handler) = CreateSender();

        await sender.SendAsync(
            CreateNotification(condition: condition, targetPriceTry: 3_700_000m, currentPriceTry: 50.2282m),
            CancellationToken.None);

        var text = ReadBody(Assert.Single(handler.Requests)).GetProperty("text").GetString();
        Assert.Contains("Fiyat alarmı: BTC", text);
        Assert.Contains($"3.700.000 TRY {expectedPhrase}", text);
        Assert.Contains("Güncel fiyat: 50,2282 TRY", text);
        Assert.Contains("Alarm no: 42", text);
    }
}
