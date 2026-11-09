# Kripto & Döviz Fiyat Takip ve Alarm Sistemi

.NET 8 Web API ile geliştirilen, kripto ve döviz fiyatlarını takip edip alarm üreten katmanlı backend projesi.

## Aşama 1 durumu
- Katmanlı solution iskeleti oluşturuldu.
- CoinGecko üzerinden BTC/USD ve BTC/TRY fiyatı çeken `GET /api/prices` endpoint'i eklendi.
- Swagger etkinleştirildi.

## Aşama 2 durumu
- `PriceHistories` entity'si ve SQL Server EF Core `DbContext` eklendi.
- İlk migration kaynak dosyası eklendi.
- TCMB `today.xml` üzerinden USD ve EUR satış kuru okuyan `TcmbProvider` eklendi.
- `GET /api/price-history` endpoint'i eklendi.
- Elle yazılmış Swagger uçları kaldırıldı; Swashbuckle entegrasyonu kullanılıyor.

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

> Not: CoinGecko ve TCMB istekleri için internet bağlantısı gerekir. SQL Server ve migration uygulama işlemi bu ortamda çalıştırılmadı.
>
> Test notu: `dotnet build` başarılıdır. Bu bilgisayarda API süreci, Windows uygulama denetiminin `CryptoFxTracker.Application.dll` yüklemesini `0x800711C7` ile engellemesi nedeniyle canlı olarak başlatılamadı. Bu nedenle `/api/prices` gerçek ağ cevabı doğrulanamadı.

## Katmanlar
- `Api`: HTTP endpoint'leri, Swagger ve uygulama başlangıcı.
- `Application`: Use-case sözleşmeleri ve arayüzler.
- `Domain`: İş alanı modelleri.
- `Infrastructure`: Harici servis erişimleri; bu aşamada CoinGecko.

## Gizli bilgiler
Bu aşamada gizli bilgi kullanılmamaktadır. İlerleyen aşamalarda bağlantı dizesi, JWT secret ve Telegram token yalnızca ortam değişkenlerinden okunacaktır.

## Paket ve test notu
EF Core SQL Server, EF Core Tools ve `Swashbuckle.AspNetCore` paket referansları eklenmiştir. Ancak bu ortamda NuGet erişimi `NU1301` hatasıyla engellendiği için paketler restore edilemedi; bu nedenle Aşama 2 build'i ve migration doğrulaması çalıştırılamadı. Elle yazılmış Swagger alternatifi eklenmemiştir.
