using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Constants;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Background;
using CryptoFxTracker.Infrastructure.Persistence;
using CryptoFxTracker.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CryptoFxTracker.IntegrationTests.Prices;

public sealed class PriceCollectorServiceTests : IDisposable
{
    private static readonly DateTime RateDate = new(2026, 11, 17);

    private readonly SqliteDatabase database = new();

    public void Dispose() => database.Dispose();

    private sealed class FakeCoinGecko : IPriceProvider
    {
        public Task<PriceQuote> GetBitcoinPriceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PriceQuote("BTC", 70_000m, 3_739_253m, DateTimeOffset.UtcNow, "CoinGecko"));
    }

    // TCMB gün içinde hep aynı yayın tarihli kurları döndürür.
    private sealed class FakeTcmb : ITcmbProvider
    {
        public Task<IReadOnlyCollection<CurrencyRate>> GetTodayRatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<CurrencyRate>>(
            [
                new CurrencyRate("USD", 50.2282m, RateDate, "TCMB"),
                new CurrencyRate("EUR", 55.9810m, RateDate, "TCMB")
            ]);
    }

    [Fact]
    public async Task AllPrices_ReachCacheAndBatch_WhenDatabaseIsUnreachable()
    {
        // Regresyon testi: TCMB kurlarının veritabanı kontrolü hata verince döngü yarıda kesiliyordu;
        // USDTRY önbelleğe giriyor, EURTRY'ye hiç sıra gelmiyordu. Docker'da API'yi veritabanı olmadan
        // çalıştırırken bulundu (ör. SQL Server container'ı henüz hazır değilken).
        var unreachable = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "db.sqlite");
        var services = new ServiceCollection()
            .AddScoped(_ => new CryptoFxTrackerDbContext(
                new DbContextOptionsBuilder<CryptoFxTrackerDbContext>().UseSqlite($"DataSource={unreachable}").Options))
            .AddScoped<IPriceProvider, FakeCoinGecko>()
            .AddScoped<ITcmbProvider, FakeTcmb>()
            .BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = new PriceUpdateChannel();
        var options = Options.Create(new PriceUpdateOptions { IntervalSeconds = 1, RetryCount = 1, RetryDelaySeconds = 0 });
        var collector = new PriceCollectorService(
            services.GetRequiredService<IServiceScopeFactory>(), cache, channel, options, NullLogger<PriceCollectorService>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await collector.StartAsync(timeout.Token);
        try
        {
            var batch = await channel.Reader.ReadAsync(timeout.Token);

            Assert.Equal(new[] { "BTC", "EURTRY", "USDTRY" }, batch.Select(price => price.Symbol).Order().ToArray());
            Assert.True(cache.TryGetValue(PriceCacheKeys.CurrentPrices, out IReadOnlyCollection<PriceQuote>? cached));
            Assert.Equal(3, cached!.Count);
        }
        finally
        {
            await collector.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TcmbRates_StayInCacheAndBatch_AfterTheyAreAlreadyInTheDatabase()
    {
        // Regresyon testi: TCMB kuru veritabanında zaten varsa önbelleğe eklenmeden atlanıyordu;
        // günün ilk döngüsünden sonra /api/prices yalnızca BTC döndürüyordu.
        var services = new ServiceCollection()
            .AddScoped<CryptoFxTrackerDbContext>(_ => database.CreateContext())
            .AddScoped<IPriceProvider, FakeCoinGecko>()
            .AddScoped<ITcmbProvider, FakeTcmb>()
            .BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = new PriceUpdateChannel();
        var options = Options.Create(new PriceUpdateOptions { IntervalSeconds = 1, RetryCount = 1, RetryDelaySeconds = 0 });
        var collector = new PriceCollectorService(
            services.GetRequiredService<IServiceScopeFactory>(), cache, channel, options, NullLogger<PriceCollectorService>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await collector.StartAsync(timeout.Token);
        try
        {
            // İkinci parti okunduğunda en az iki döngü bitmiştir; ikincide TCMB satırları zaten veritabanında.
            await channel.Reader.ReadAsync(timeout.Token);
            var laterBatch = await channel.Reader.ReadAsync(timeout.Token);

            Assert.Equal(
                new[] { "BTC", "EURTRY", "USDTRY" },
                laterBatch.Select(price => price.Symbol).Order().ToArray());

            Assert.True(cache.TryGetValue(PriceCacheKeys.CurrentPrices, out IReadOnlyCollection<PriceQuote>? cached));
            Assert.Contains(cached!, price => price.Symbol == "USDTRY" && price.Try == 50.2282m);
            Assert.Contains(cached!, price => price.Symbol == "EURTRY");

            // Toplayıcı partiyi kuyruğa koyup SONRA kaydediyor. Durdurmadan önce bir parti daha beklenir:
            // üçüncü döngü ancak ikincinin kaydı bitince başlar. Yoksa StopAsync ikinci kaydı yarıda
            // iptal edebilir ve aşağıdaki BTC sayımı ara sıra kırılır.
            await channel.Reader.ReadAsync(timeout.Token);
        }
        finally
        {
            await collector.StopAsync(CancellationToken.None);
        }

        // Aynı yayın tarihli kur veritabanına bir kez yazılır; BTC her döngüde yazılır.
        using var context = database.CreateContext();
        Assert.Equal(1, context.PriceHistories.Count(price => price.Symbol == "USDTRY"));
        Assert.Equal(1, context.PriceHistories.Count(price => price.Symbol == "EURTRY"));
        Assert.True(context.PriceHistories.Count(price => price.Symbol == "BTC") >= 2);
    }
}
