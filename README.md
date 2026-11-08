# Kripto & Döviz Fiyat Takip ve Alarm Sistemi

.NET 8 Web API ile geliştirilen, kripto ve döviz fiyatlarını takip edip alarm üreten katmanlı backend projesi.

## Aşama 1 durumu
- Katmanlı solution iskeleti oluşturuldu.
- CoinGecko üzerinden BTC/USD ve BTC/TRY fiyatı çeken `GET /api/prices` endpoint'i eklendi.
- Swagger etkinleştirildi.

## Çalıştırma
```powershell
dotnet restore
dotnet build
dotnet run --project src/CryptoFxTracker.Api
```

Swagger: `http://localhost:5000/swagger`

Örnek istek:
```powershell
Invoke-RestMethod http://localhost:5000/api/prices
```

> Not: CoinGecko isteği için internet bağlantısı gerekir. SQL Server, Docker ve gerçek fiyat geçmişi henüz Aşama 1 kapsamına dahil değildir.
>
> Test notu: `dotnet build` başarılıdır. Bu bilgisayarda API süreci, Windows uygulama denetiminin `CryptoFxTracker.Application.dll` yüklemesini `0x800711C7` ile engellemesi nedeniyle canlı olarak başlatılamadı. Bu nedenle `/api/prices` gerçek ağ cevabı doğrulanamadı.

## Katmanlar
- `Api`: HTTP endpoint'leri, Swagger ve uygulama başlangıcı.
- `Application`: Use-case sözleşmeleri ve arayüzler.
- `Domain`: İş alanı modelleri.
- `Infrastructure`: Harici servis erişimleri; bu aşamada CoinGecko.

## Gizli bilgiler
Bu aşamada gizli bilgi kullanılmamaktadır. İlerleyen aşamalarda bağlantı dizesi, JWT secret ve Telegram token yalnızca ortam değişkenlerinden okunacaktır.

## Swagger notu
NuGet erişimi ortam politikası tarafından engellendiği için Swashbuckle paketi indirilemedi. Buna rağmen OpenAPI JSON `/swagger/v1/swagger.json`, tarayıcı arayüzü `/swagger` altında uygulama içinde sunulmaktadır.
