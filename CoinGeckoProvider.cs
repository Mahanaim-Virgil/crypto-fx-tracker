using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Infrastructure.CoinGecko;

public sealed class CoinGeckoProvider(HttpClient httpClient) : IPriceProvider
{
    private const string Endpoint = "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd,try";

    public async Task<PriceQuote> GetBitcoinPriceAsync(CancellationToken cancellationToken = default)
    {
        CoinGeckoResponse? response;

        try
        {
            response = await httpClient.GetFromJsonAsync<CoinGeckoResponse>(Endpoint, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("CoinGecko fiyat servisine ulaşılamadı.", exception);
        }

        if (response?.Bitcoin is null || response.Bitcoin.Usd is null || response.Bitcoin.Try is null)
        {
            throw new InvalidOperationException("CoinGecko beklenen BTC fiyat alanlarını döndürmedi.");
        }

        return new PriceQuote(
            Symbol: "BTC",
            Usd: response.Bitcoin.Usd.Value,
            Try: response.Bitcoin.Try.Value,
            RetrievedAtUtc: DateTimeOffset.UtcNow,
            Provider: "CoinGecko");
    }

    private sealed class CoinGeckoResponse
    {
        [JsonPropertyName("bitcoin")]
        public BitcoinPrice? Bitcoin { get; init; }
    }

    private sealed class BitcoinPrice
    {
        [JsonPropertyName("usd")]
        public decimal? Usd { get; init; }

        [JsonPropertyName("try")]
        public decimal? Try { get; init; }
    }
}
