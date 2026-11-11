namespace CryptoFxTracker.Application.Constants;

// Fiyat toplayıcının ürettiği semboller. Alarm yalnızca bu sembollere kurulabilir;
// fiyatı hiç toplanmayan bir sembole kurulan alarm asla tetiklenmezdi.
public static class PriceSymbols
{
    public const string Btc = "BTC";
    public const string UsdTry = "USDTRY";
    public const string EurTry = "EURTRY";

    public static readonly IReadOnlyList<string> Supported = [Btc, UsdTry, EurTry];
}
