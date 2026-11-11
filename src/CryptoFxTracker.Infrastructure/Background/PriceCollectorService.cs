using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Constants;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Domain.Entities;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CryptoFxTracker.Infrastructure.Background;

public sealed class PriceCollectorService(
    IServiceScopeFactory scopeFactory,
    IMemoryCache memoryCache,
    PriceUpdateChannel priceUpdates,
    IOptions<PriceUpdateOptions> options,
    ILogger<PriceCollectorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSeconds));

        logger.LogInformation(
            "Fiyat toplama servisi başladı. Aralık: {IntervalSeconds} saniye, retry: {RetryCount}",
            interval.TotalSeconds,
            settings.RetryCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectPricesSafelyAsync(settings, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Fiyat toplama döngüsünde beklenmeyen hata; servis sonraki döngüde devam edecek.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Fiyat toplama servisi durdu.");
    }

    private async Task CollectPricesSafelyAsync(PriceUpdateOptions settings, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CryptoFxTrackerDbContext>();
        var coinGecko = scope.ServiceProvider.GetRequiredService<IPriceProvider>();
        var tcmb = scope.ServiceProvider.GetRequiredService<ITcmbProvider>();
        var currentPrices = new List<PriceQuote>();

        try
        {
            var bitcoin = await ExecuteWithRetryAsync(
                () => coinGecko.GetBitcoinPriceAsync(cancellationToken),
                "CoinGecko",
                settings,
                cancellationToken);

            dbContext.PriceHistories.Add(new PriceHistory
            {
                Symbol = bitcoin.Symbol,
                Provider = bitcoin.Provider,
                PriceUsd = bitcoin.Usd,
                PriceTry = bitcoin.Try,
                RecordedAtUtc = bitcoin.RetrievedAtUtc
            });
            currentPrices.Add(bitcoin);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "CoinGecko fiyatı alınamadı; TCMB ve sonraki döngü devam edecek.");
        }

        try
        {
            var rates = await ExecuteWithRetryAsync(
                () => tcmb.GetTodayRatesAsync(cancellationToken),
                "TCMB",
                settings,
                cancellationToken);

            foreach (var rate in rates)
            {
                var symbol = $"{rate.CurrencyCode}TRY";
                var recordedAtUtc = new DateTimeOffset(DateOnly.FromDateTime(rate.RateDate), TimeOnly.MinValue, TimeSpan.Zero);

                // Önbellek her döngüde baştan yazıldığı için kur, veritabanında zaten olsa da
                // önbelleğe eklenmeli; yoksa günün ilk döngüsünden sonra önbellekten düşer.
                currentPrices.Add(new PriceQuote(
                    Symbol: symbol,
                    Usd: 1m,
                    Try: rate.TryValue,
                    RetrievedAtUtc: DateTimeOffset.UtcNow,
                    Provider: rate.Provider));

                var exists = await dbContext.PriceHistories.AnyAsync(
                    price => price.Symbol == symbol && price.RecordedAtUtc == recordedAtUtc,
                    cancellationToken);

                if (exists)
                {
                    continue;
                }

                dbContext.PriceHistories.Add(new PriceHistory
                {
                    Symbol = symbol,
                    Provider = rate.Provider,
                    PriceUsd = null,
                    PriceTry = rate.TryValue,
                    RecordedAtUtc = recordedAtUtc
                });
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "TCMB kurları alınamadı; CoinGecko kaydı ve sonraki döngü devam edecek.");
        }

        if (currentPrices.Count > 0)
        {
            var snapshot = currentPrices.AsReadOnly();
            memoryCache.Set(PriceCacheKeys.CurrentPrices, snapshot);

            // Alarm servisine haber ver; kuyruk doluysa eski parti atılır, toplayıcı beklemez.
            priceUpdates.Publish(snapshot);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                var writtenCount = await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("{Count} fiyat kaydı veritabanına yazıldı.", writtenCount);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Fiyat kayıtları veritabanına yazılamadı; servis sonraki döngüde devam edecek.");
            }
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        string source,
        PriceUpdateOptions settings,
        CancellationToken cancellationToken)
    {
        var retryCount = Math.Max(1, settings.RetryCount);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
                if (attempt == retryCount)
                {
                    break;
                }

                logger.LogWarning(
                    exception,
                    "{Source} isteği başarısız oldu. Deneme {Attempt}/{RetryCount}; {DelaySeconds} saniye sonra yeniden denenecek.",
                    source,
                    attempt,
                    retryCount,
                    settings.RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, settings.RetryDelaySeconds)), cancellationToken);
            }
        }

        throw new InvalidOperationException($"{source} için {retryCount} deneme de başarısız oldu.", lastException);
    }
}
