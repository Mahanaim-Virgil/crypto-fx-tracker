using System.Threading.Channels;
using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Infrastructure.Background;

// Fiyat toplayıcı (yazan) ile alarm servisi (okuyan) arasındaki kuyruk.
// Kapasite 1 ve DropOldest: toplayıcı hiçbir zaman beklemez; alarm servisi geride kalırsa
// (ör. bildirim yavaşsa) yalnızca en güncel fiyat partisi işlenir, eski fiyatlar atılır.
public sealed class PriceUpdateChannel
{
    private readonly Channel<IReadOnlyCollection<PriceQuote>> channel =
        Channel.CreateBounded<IReadOnlyCollection<PriceQuote>>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

    public ChannelReader<IReadOnlyCollection<PriceQuote>> Reader => channel.Reader;

    public void Publish(IReadOnlyCollection<PriceQuote> prices) => channel.Writer.TryWrite(prices);
}
