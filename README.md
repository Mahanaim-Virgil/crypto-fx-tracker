# Crypto & FX Tracker

[![CI](https://github.com/Mahanaim-Virgil/crypto-fx-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/Mahanaim-Virgil/crypto-fx-tracker/actions/workflows/ci.yml)

Kripto ve döviz fiyatlarını takip eden, kullanıcıların fiyat alarmı kurabildiği bir .NET 8 Web API.
CoinGecko'dan BTC fiyatını, TCMB'den USD/TRY ve EUR/TRY kurlarını dakikada bir toplar, geçmişi SQL Server'da saklar
ve kullanıcının belirlediği eşik aşılınca Telegram'dan bildirim gönderir.

**Teknolojiler:** C#, .NET 8, ASP.NET Core (Minimal API), Entity Framework Core, SQL Server, JWT, Swagger, Telegram Bot API, xUnit, GitHub Actions

## Özellikler

- Arka planda dakikada bir fiyat toplama (CoinGecko + TCMB), dış API hatalarında sınırlı yeniden deneme
- Güncel fiyatların bellekte önbelleklenmesi: `GET /api/prices` dış API'ye gitmeden yanıt verir
- Fiyat geçmişi (TCMB'nin günlük kuru aynı gün için tekrar yazılmaz)
- JWT ile kayıt ve giriş, `PasswordHasher` ile şifre hash'leme
- Kullanıcıya özel takip listesi
- Fiyat alarmları: "USDTRY 45 TL'nin üstüne çıkınca" gibi TL bazlı koşullar (`Above` / `Below`), arka planda kontrol
- Tek seferlik alarm: koşul sağlanınca Telegram mesajı gider, alarm pasif olur; bildirim gitmezse sonraki fiyatta tekrar denenir
- Kullanıcıya özel Telegram sohbeti; bot token verilmezse bildirimler yalnızca loga yazılır

## Mimari

```
CryptoFxTracker.Api             HTTP uç noktaları, JWT doğrulaması, Swagger, uygulama başlangıcı
CryptoFxTracker.Application     Arayüzler (IPriceProvider, ITcmbProvider, IAuthService...) ve modeller
CryptoFxTracker.Domain          Entity'ler ve iş kuralları (ör. PriceAlert.IsTriggeredBy)
CryptoFxTracker.Infrastructure  EF Core, CoinGecko/TCMB istemcileri, arka plan servisleri
```

Akış:

```
PriceCollectorService ──(60 sn)──> CoinGecko / TCMB
        │  fiyatları yazar
        ├──> SQL Server (PriceHistories)
        ├──> IMemoryCache ──> GET /api/prices
        └──> PriceUpdateChannel ──> AlertCheckerService ──> IAlertNotificationSender
                                                                ├── Telegram (bot token varsa)
                                                                └── Log (yoksa)
```

### Tasarım kararları

Bütün kararlar, seçenekleri ve takaslarıyla: [docs/MIMARI-KARARLAR.md](docs/MIMARI-KARARLAR.md). Özetle:

- **Toplayıcı ile alarm kontrolü bir kuyrukla ayrıldı.** Fiyat toplayıcı her partiyi kapasitesi 1 olan bir
  `Channel`'a bırakır ve beklemez; alarm servisi kendi hızında okur. Bildirim yavaşlarsa fiyat toplama etkilenmez.
  Takas: alarm servisi geride kalırsa ara partiler atlanır, sadece en güncel fiyat kontrol edilir.
- **`DbContext` arka plan servisine constructor'dan verilmez.** `BackgroundService` singleton, `DbContext` scoped
  olduğu için her döngüde `IServiceScopeFactory` ile yeni scope açılır.
- **Alarm koşulu entity'de.** "Tetiklendi mi?" kuralı SQL'de değil `PriceAlert.IsTriggeredBy` içinde, tek yerde.
  Takas: bir sembolün tüm aktif alarmları belleğe okunur; alarm sayısı çok büyürse sorgu SQL'e taşınmalı.
- **Alarm, bildirim gittikten sonra pasif olur (en az bir kez teslim).** Her alarm ayrı kaydedilir. Kayıt başarısız
  olursa aynı bildirim iki kez gidebilir; hiç gitmemesi daha kötü olduğu için bu takas kabul edildi.
- **Gizli anahtarlar kodda yok.** JWT anahtarı `Jwt__Secret`, Telegram bot token'ı `Telegram__BotToken` ortam
  değişkeninden okunur. Telegram'da token istek adresinin içinde olduğu için o istemcinin HTTP logları kapatıldı.

## Uç noktalar

| Metot | Yol | Kimlik doğrulama | Açıklama |
| --- | --- | --- | --- |
| GET | `/api/prices` | — | Güncel fiyatlar (önbellekten) |
| GET | `/api/price-history` | — | Fiyat geçmişi, en yeniden eskiye |
| POST | `/api/auth/register` | — | Kayıt |
| POST | `/api/auth/login` | — | Giriş, JWT döner |
| GET | `/api/watchlist` | JWT | Takip listesini getir |
| POST | `/api/watchlist/{symbol}` | JWT | Sembol ekle |
| DELETE | `/api/watchlist/{symbol}` | JWT | Sembol sil |
| POST | `/api/alerts` | JWT | Alarm oluştur |
| GET | `/api/alerts` | JWT | Alarmları listele |
| DELETE | `/api/alerts/{id}` | JWT | Alarm sil |
| PUT | `/api/users/me/telegram` | JWT | Bildirimlerin gideceği Telegram ChatId'sini ayarla (`null` ile kaldır) |

## Çalıştırma

```bash
git clone https://github.com/Mahanaim-Virgil/crypto-fx-tracker.git
cd crypto-fx-tracker
```

### Docker ile (önerilen)

Gereksinim: Docker (Docker Desktop). SQL Server, migration'lar ve API tek komutla kalkar.

1. `.env.example` dosyasını `.env` adıyla kopyalayın ve en az `MSSQL_SA_PASSWORD` (büyük/küçük harf, rakam,
   sembol) ile `Jwt__Secret` (en az 32 karakter) değerlerini doldurun. `.env` git'e girmez.
   Değerlerde `$`, `'`, `"`, `#` ve boşluk kullanmayın: compose `$`'ı değişken sanıp siler, tırnaklar bağlantı
   dizesini bozar. Güvenli semboller `.env.example`'da listeli.
2. Başlatın:

```bash
docker compose up --build
```

Sıra: SQL Server sağlıklı olunca `migrator` migration'ları uygular ve çıkar, ardından API başlar.
Swagger: http://localhost:8080/swagger, sağlık kontrolü: http://localhost:8080/health.
Durdurmak için `docker compose down` (veritabanı volume'u kalır; silmek için `docker compose down -v`).

### Docker olmadan (Windows, LocalDB)

Gereksinimler: .NET SDK 9.0.200 veya üstü (çözüm dosyası `.slnx` biçiminde; SDK 8 bunu okuyamaz) ve .NET 8
çalışma zamanı, SQL Server LocalDB (Visual Studio ile gelir), `dotnet-ef` aracı.

JWT anahtarını en az 32 karakter olacak şekilde ortam değişkeni olarak verin (Windows `cmd`):

```bat
set Jwt__Secret=buraya-en-az-32-karakterlik-rastgele-bir-anahtar
```

Veritabanını oluşturup API'yi başlatın:

```bash
dotnet ef database update --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Infrastructure
dotnet run --project src/CryptoFxTracker.Api --urls http://localhost:5000
```

Swagger: http://localhost:5000/swagger — giriş yaptıktan sonra dönen token'ı **Authorize** butonuna yapıştırın.

Telegram bildirimi isteğe bağlıdır. @BotFather'dan bir bot açın, bota `/start` yazın ve API'yi başlatmadan önce:

```bat
set Telegram__BotToken=botfather-tokeni
set Telegram__ChatId=varsayilan-sohbet-id
```

Bot token verilmezse alarmlar yine çalışır, bildirimler yalnızca loga yazılır. Değişkenlerin açıklaması: [.env.example](.env.example).

Bağlantı dizesi ve toplama aralığı `src/CryptoFxTracker.Api/appsettings.json` içindedir.

## Testler

```bash
dotnet test CryptoFxTracker.slnx
```

Her push'ta GitHub Actions'ta (Ubuntu) derlenir ve çalışır. Çözüm dosyası `.slnx` olduğu için SDK 9.0.200 veya
üstü gerekir (SDK 8 ile projeleri tek tek verin: `dotnet test tests/CryptoFxTracker.UnitTests/CryptoFxTracker.UnitTests.csproj`).
`CryptoFxTracker.DatabaseTests` Docker gerektirir.

| Proje | Ne test ediliyor | Nasıl |
| --- | --- | --- |
| `CryptoFxTracker.UnitTests` | `PriceAlert` kuralı (Above `>=`, Below `<=`, tek seferlik), Telegram göndericisi (istek adresi, ChatId seçimi, hata mesajları, Türkçe sayı biçimi) | Ağ ve veritabanı yok; HTTP için isteği kaydeden sahte `HttpMessageHandler` |
| `CryptoFxTracker.IntegrationTests` | Alarm işleme (bir kez çalma, gönderim hatasında aktif kalma, bildirim sırasında silinen alarm), fiyat toplayıcı, `/api/alerts` ve `/api/users/me/telegram` | Bellekte SQLite; API için `WebApplicationFactory` (arka plan servisleri kapalı, dış servislere istek gitmez) |
| `CryptoFxTracker.DatabaseTests` | Migration'ların boş SQL Server'a uygulanması, unutulmuş migration kontrolü, `decimal(20,8)` yuvarlaması, `DateTimeOffset` sıralaması | Testcontainers ile gerçek SQL Server 2022 container'ı |

Geliştirme sırasında bulunan hataların her biri için bir regresyon testi var (ör. Telegram adresinin mutlak
URI sayılması, 8'den fazla ondalığın sessizce yuvarlanması, TCMB kurlarının önbellekten düşmesi, veritabanı
kapalıyken kurların önbelleğe girmemesi). Sonuncusunun, hatalı kod geri getirildiğinde gerçekten kırıldığı görüldü.

## Yol haritası

Proje aşamalarla geliştiriliyor; her tamamlanan aşamanın doğrulanmış son commit'i `stage-N` etiketiyle işaretli.

| Aşama | İçerik | Durum |
| --- | --- | --- |
| 1 | Katmanlı iskelet, CoinGecko, Swagger | Tamamlandı (`stage-1`) |
| 2 | EF Core, fiyat geçmişi, TCMB | Tamamlandı (`stage-2`) |
| 3 | Arka plan toplayıcı, önbellek, retry | Tamamlandı (`stage-3`) |
| 4 | Kayıt/giriş, JWT, takip listesi | Tamamlandı (`stage-4`) |
| 5 | Fiyat alarmları, Telegram bildirimi | Tamamlandı (`stage-5`) |
| 6 | xUnit testleri, GitHub Actions CI | Tamamlandı (`stage-6`) |
| 7 | Docker, docker compose, SQL Server testleri, Mimari Kararlar | Tamamlandı (`stage-7`) |

Ayrıntılı ilerleme ve doğrulama notları: [PROGRESS.md](PROGRESS.md). Her aşamada öğrendiklerim: [LEARNING.md](LEARNING.md).
Mimari kararlar: [docs/MIMARI-KARARLAR.md](docs/MIMARI-KARARLAR.md).
