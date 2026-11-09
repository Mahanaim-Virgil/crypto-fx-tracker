using System.Globalization;
using System.Xml.Linq;
using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Application.Models;

namespace CryptoFxTracker.Infrastructure.Tcmb;

public sealed class TcmbProvider(HttpClient httpClient) : ITcmbProvider
{
    private const string Endpoint = "https://www.tcmb.gov.tr/kurlar/today.xml";

    public async Task<IReadOnlyCollection<CurrencyRate>> GetTodayRatesAsync(CancellationToken cancellationToken = default)
    {
        XDocument document;

        try
        {
            await using var stream = await httpClient.GetStreamAsync(Endpoint, cancellationToken);
            document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("TCMB kur servisine ulaşılamadı.", exception);
        }

        var dateText = document.Root?.Attribute("Tarih")?.Value;
        var rateDate = DateTime.TryParse(dateText, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var parsedDate)
            ? parsedDate.Date
            : DateTime.UtcNow.Date;

        var rates = document.Root?
            .Elements("Currency")
            .Where(currency => currency.Attribute("Kod")?.Value is "USD" or "EUR")
            .Select(currency =>
            {
                var code = currency.Attribute("Kod")!.Value;
                var valueText = currency.Element("ForexSelling")?.Value;
                if (!decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    throw new InvalidOperationException($"TCMB {code} ForexSelling değeri okunamadı.");
                }

                return new CurrencyRate(code, value, rateDate, "TCMB");
            })
            .ToArray() ?? [];

        if (rates.Length == 0)
        {
            throw new InvalidOperationException("TCMB cevabında USD veya EUR kuru bulunamadı.");
        }

        return rates;
    }
}
