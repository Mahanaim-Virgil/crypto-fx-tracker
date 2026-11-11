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

## Aşama 3 durumu
- `PriceCollectorService` ile CoinGecko ve TCMB fiyatları varsayılan 60 saniyede bir toplanır.
- Başarılı fiyatlar `PriceHistories` tablosuna kaydedilir.
- Güncel fiyatlar `IMemoryCache` içinde tutulur; `GET /api/prices` cache'ten cevap verir.
- Her kaynak için sınırlı retry ve hata izolasyonu uygulanır. Bir kaynak başarısız olsa da servis sonraki döngüde devam eder.
- Aralık ve retry ayarları `PriceUpdate` bölümünden değiştirilebilir.

## Aşama 4 durumu
- Kullanıcı kaydı ve girişi eklendi.
- Parolalar `PasswordHasher<User>` ile hashlenir; düz metin parola saklanmaz.
- JWT üretimi ve Bearer doğrulaması eklendi.
- Giriş yapmış kullanıcılar takip listesine sembol ekleyebilir, silebilir ve listeleyebilir.
- `PriceUsd` nullable yapıldı; TCMB kayıtlarında USD fiyatı artık sabit `1` yazılmaz.
- TCMB kayıtlarında aynı `Symbol + RecordedAtUtc` varsa tekrar kayıt eklenmez.

### Aşama 4 endpoint'leri
```http
POST /api/auth/register
POST /api/auth/login
GET /api/watchlist
POST /api/watchlist/{symbol}
DELETE /api/watchlist/{symbol}
```

JWT secret kodda tutulmaz. `Jwt__Secret` ortam değişkeniyle, en az 32 karakter olacak şekilde verilmelidir.

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

> Not: CoinGecko ve TCMB istekleri için internet bağlantısı, fiyat geçmişi için SQL Server gerekir.
>
> Test notu: Aşama 4 yerel makinede derlendi, migration uygulandı ve kayıt/giriş/takip listesi uç noktaları Swagger üzerinden test edildi.

## Katmanlar
- `Api`: HTTP endpoint'leri, Swagger ve uygulama başlangıcı.
- `Application`: Use-case sözleşmeleri ve arayüzler.
- `Domain`: İş alanı modelleri.
- `Infrastructure`: Harici servis erişimleri; bu aşamada CoinGecko.

## Gizli bilgiler
Bu aşamada gizli bilgi kullanılmamaktadır. İlerleyen aşamalarda bağlantı dizesi, JWT secret ve Telegram token yalnızca ortam değişkenlerinden okunacaktır.

## Paket ve test notu
EF Core SQL Server, EF Core Tools ve `Swashbuckle.AspNetCore` paket referansları projededir. Swagger arayüzü JWT desteğiyle `/swagger` adresinde çalışır.

### Aşama 4 migration notu

Kullanıcı ve takip listesi tabloları ile nullable `PriceUsd` değişikliği için `AddUsersWatchlistAndNullablePriceUsd` migration'ı eklendi. Uygulamak için:

dotnet ef database update --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Api

Önkoşul: `Jwt__Secret` ortam değişkeni tanımlı olmalı (en az 32 karakter). Uygulama başlarken bunu kontrol eder, `dotnet ef` de uygulamayı başlattığı için bu değişken olmadan çalışmaz.

### Aşama 4 paketleri
- `src/CryptoFxTracker.Api`: `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11
- `src/CryptoFxTracker.Infrastructure`: `Microsoft.Extensions.Identity.Core` 8.0.11 ve `System.IdentityModel.Tokens.Jwt` 7.7.1

Aşama 4 yerel makinede doğrulandı (ayrıntılar PROGRESS.md içinde).
