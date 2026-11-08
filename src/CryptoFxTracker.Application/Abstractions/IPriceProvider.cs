using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Application.Abstractions;

public interface IPriceProvider
{
    Task<PriceQuote> GetBitcoinPriceAsync(CancellationToken cancellationToken = default);
}
