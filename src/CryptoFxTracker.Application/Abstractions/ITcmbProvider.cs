using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Application.Abstractions;

public interface ITcmbProvider
{
    Task<IReadOnlyCollection<CurrencyRate>> GetTodayRatesAsync(CancellationToken cancellationToken = default);
}
