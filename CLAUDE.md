# CLAUDE.md

## Proje
Kripto & Döviz Fiyat Takip ve Alarm Sistemi. .NET 8 Web API, katmanlı yapı:
`src/CryptoFxTracker.Api`, `Application`, `Domain`, `Infrastructure`.
EF Core + MSSQL LocalDB, CoinGecko (`IPriceProvider`) ve TCMB (`ITcmbProvider`)
sağlayıcıları, `PriceCollectorService` (BackgroundService, 60 sn), `IMemoryCache`,
Swashbuckle, JWT kimlik doğrulama (`PasswordHasher<User>`).

## Aşamalar
- Tamamlandı (`stage-1` ... `stage-7` etiketleri): 1 iskelet + CoinGecko,
  2 EF Core + TCMB, 3 arka plan toplayıcı + cache + retry, 4 kayıt/giriş + JWT + takip listesi,
  5 fiyat alarmı (`Channel` + `AlertCheckerService`) + Telegram bildirimi,
  6 xUnit testleri (birim + SQLite/WebApplicationFactory entegrasyon) + GitHub Actions CI,
  7 Docker (Dockerfile, compose, migration bundle, `/health`) + Testcontainers SQL Server testleri
  + `docs/MIMARI-KARARLAR.md`.
- Planlanmış aşama kalmadı. Yeni bir karar alınırsa `docs/MIMARI-KARARLAR.md`'ye aynı biçimde
  (bağlam, seçenekler, karar, takas) eklenir.
- Her aşama bitince `stage-N` etiketi, aşamanın doğrulanmış son commit'ine konur.

## Geliştirici ve çalışma şekli
Geliştirici 3. sınıf yazılım mühendisliği öğrencisi, hedefi backend (C#/ASP.NET Core)
staj/iş. Proje portföy ve mülakat için: "çalışıyor" yetmez, neden öyle yapıldığı anlaşılmalı.
- Türkçe, sade ve dürüst anlat.
- Değişiklikleri küçük adımlarla yap, her adımda neden öyle yaptığını söyle.
- "Çalışıyor" deme: neyi çalıştırdığını, neyi çalıştıramadığını açıkça yaz.
- Geri alınamaz işlemlerden önce (DELETE/UPDATE, force push, migration uygulama)
  ne yapacağını göster ve onay iste.
- Mimari karar alırken seçenekleri ve takasları kısaca yaz (Aşama 7 "Mimari Kararlar" için).

## Ortam (Windows)
- Terminal `cmd` (PowerShell değil). Satır devamı `^`. Arama için `findstr`:
  Türkçe karakterli kelimeleri güvenilir eşleştirmez, ASCII kelime ara.
- Windows Akıllı Uygulama Denetimi imzasız exe'leri, bazen derlenmiş dll'leri de
  engelliyor (`0x800711C7`). Kapatmayı önerme, geri açılamaz.
- API'yi şöyle çalıştır: `cd src\CryptoFxTracker.Api`, sonra
  `dotnet bin\Debug\net8.0\CryptoFxTracker.Api.dll --urls http://localhost:5000`.
  Engel aralıklı. Düz `dotnet build` işe yaramaz: derleme deterministik ve değişmeyen proje
  yeniden derlenmez, aynı engelli dll kalır. `dotnet build --no-incremental -p:Deterministic=false`
  ile her derlemede farklı dll üret, sonra tekrar dene. `dotnet ef` çalıştırırken `--no-build`
  ver ki geçen dll'ler yeniden derlenip değişmesin.
- Swagger: `http://localhost:5000/swagger`.
- Docker Desktop kurulu (kullanıcı klasörüne: `%LOCALAPPDATA%\Programs\DockerDesktop\resources\bin`).
  Container'lar Linux'ta çalıştığı için Akıllı Uygulama Denetimi onları etkilemez; API ve testler için
  en güvenilir yerel yol Docker'dır. Docker'dan önce açılmış bir kabukta `docker` PATH'te olmayabilir.

## Komutlar
- Derle: `dotnet build` (kök klasörde).
- Docker ile her şey: kök klasörde `.env` (bkz. `.env.example`; `MSSQL_SA_PASSWORD`, `Jwt__Secret`
  zorunlu), sonra `docker compose up --build`. Swagger `http://localhost:8080/swagger`, `/health`.
  Sıra: `sqlserver` (sağlıklı) → `migrator` (EF migration bundle, bir kez) → `api`. SQL Server
  portu dışarı açık değil. `docker compose down -v` veritabanı volume'unu da siler (geri alınamaz).
- Testler: `tests/CryptoFxTracker.UnitTests` (ağ/DB yok), `tests/CryptoFxTracker.IntegrationTests`
  (bellekte SQLite, `WebApplicationFactory`), `tests/CryptoFxTracker.DatabaseTests` (Testcontainers ile
  gerçek SQL Server; Docker gerekir). Windows'ta `dotnet test` test dll'leri engellendiği için
  çalışmaz. Güvenilir sonuç GitHub Actions'tan (`gh run list --branch <dal>`, `gh run view <id> --log`)
  veya yerelde Linux container'ında: repoyu `mcr.microsoft.com/dotnet/sdk:8.0` container'ına salt-okunur
  bağla, içeride bir kopyaya `cp -r` yap, `bin`/`obj`'yi sil, `dotnet test <proje>.csproj` çalıştır
  (SDK 8 `.slnx` okuyamaz; projeleri tek tek ver). `DatabaseTests` için container'a Docker soketini de
  bağla ve Testcontainers'a host'u göster: `-v //var/run/docker.sock:/var/run/docker.sock`
  `-e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` (Git Bash'te `MSYS_NO_PATHCONV=1` ile).
  NuGet önbelleği için `-v cfx-nuget:/root/.nuget/packages` aramaları hızlandırır.
- `.env` değerlerinde `$`, `'`, `"`, `#`, boşluk kullanma: compose `$`'ı değişken sanar, tırnaklar
  bağlantı dizesini bozar. Compose'daki bağlantı dizesinde şifre tek tırnak içinde (`;` güvenli).
- Unit/Integration testlerinde migration değil `EnsureCreated` kullanılır; SQLite `DateTimeOffset` ile
  sıralayamaz. Gerçek migration'lar ve SQL Server davranışı `DatabaseTests`'te sınanır.
- `Jwt__Secret` ortam değişkeni (en az 32 karakter) yoksa API açılmaz. Her yeni cmd penceresinde
  `set Jwt__Secret=...` gerekir. Gizli anahtarı koda, repoya veya belgeye yazma.
- `dotnet ef` artık `DesignTimeDbContextFactory` kullanır; `--startup-project` olarak Infrastructure
  verilirse `Program.cs` çalışmaz ve `Jwt__Secret` gerekmez. Bağlantı dizesi `ConnectionStrings__Default`
  ortam değişkeninden, yoksa LocalDB'den. (Docker'daki bundle derlemesinde doğrulandı; Windows'ta
  yerelde denenmedi.)
- Telegram isteğe bağlı: `Telegram__BotToken` (yoksa bildirimler yalnızca loga gider) ve
  `Telegram__ChatId` (varsayılan sohbet, sayı olmalı; değilse API açılmaz). Token istek
  adresinde olduğu için Telegram HttpClient'ının logları kapalı; bunu geri açma. Token ekran
  görüntüsüne girerse BotFather'da `/revoke` ile yenilenmeli.
- Migration ekle: `dotnet ef migrations add ADI --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Infrastructure --output-dir Persistence/Migrations`
- Migration uygula (LocalDB): `dotnet ef database update --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Infrastructure`
  Docker'da migration'ları `migrator` servisi uygular. Yeni migration eklenince `DatabaseTests`'teki
  "unutulmuş migration" testi model ile snapshot'ı karşılaştırır.
- Sorgu: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CryptoFxTracker -C -Q "..."`
- Migration `dotnet ef` ile üretilir, elle yazılmaz. Migration dosyaları ve
  `CryptoFxTrackerDbContextModelSnapshot.cs` birlikte commit edilir.

## Veritabanı notları
- Tablolar: `PriceHistories` (Id, Symbol, Provider, PriceUsd nullable, PriceTry, RecordedAtUtc),
  `Users` (e-posta unique, `TelegramChatId` nullable), `UserWatchlists` (birleşik anahtar UserId + Symbol),
  `PriceAlerts` (Symbol, Condition metin `Above`/`Below`, TargetPriceTry `decimal(20,8)`, IsActive,
  TriggeredAtUtc, TriggeredPriceTry; indeks `Symbol + IsActive`), `__EFMigrationsHistory`.
- Desteklenen semboller `PriceSymbols` sabitinde: `BTC`, `USDTRY`, `EURTRY` (önbellek ve veritabanı aynı adları kullanır).
- LocalDB'de testlerden kalan `@example.test` kullanıcıları ve alarmları var; gerçek veri değil.
- CoinGecko: `Symbol=BTC`, dakikada bir satır. TCMB: `USDTRY`, `EURTRY`, `PriceUsd` NULL,
  `RecordedAtUtc` kurun yayın tarihi (gece yarısı UTC). Aynı `Symbol + RecordedAtUtc`
  varsa yeni satır yazılmaz (kontrol uygulama katmanında).
- Kural: `BackgroundService` singleton, `DbContext` scoped'dır. `DbContext` constructor'a
  verilmez, `IServiceScopeFactory` ile scope açılır.

## Git
- Commit mesajı `feat:`, `fix:`, `docs:`, `test:`, `ci:` önekiyle; küçük ve tek konulu commit.
- Aşama işleri `stage-N-...` dalında yapılabilir; dal CI'da yeşilse `main`'e birleştirilir.
  `main`'e birleştirme ve `main` push'u ayrıca onay ister.
- Push etmeden önce sor. Geçmiş yeniden yazma (force push) ayrıca onay ister.
- Git kimliği global ayardan gelir (GitHub noreply adresi). Repoya yerel `user.name` veya
  `user.email` yazma.
- Geçmiş 2026-11-16'da `filter-branch` ile düzeltildi (tüm commit ve tag'ler noreply adresinde),
  force push edildi ve repo public yapıldı. Yedek: Masaüstünde `crypto-fx-tracker-backup-2026-11-16.*`.

## Bilinen eksikler
- Girdi doğrulaması (e-posta biçimi, şifre uzunluğu) test edilmedi.
- Testler SQL Server'da değil SQLite'ta çalışıyor; SQL Server'a özgü davranış (ör. `decimal(20,8)`
  yuvarlaması, `DateTimeOffset` sıralaması) testlerle doğrulanmıyor. Aşama 7'de Testcontainers ile
  gerçek SQL Server düşünülebilir. Auth ve watchlist endpoint'lerinin testi yok.
- Takip listesine aynı sembol ikinci kez eklenince 409 değil 200 dönüyor (idempotent, bilinçli).
- Swagger'da eski endpoint'lerin (auth, watchlist) 401, 409, 204 yanıtları belgelenmemiş;
  `/api/alerts` ve `/api/users/me/telegram` belgelendi.
- JWT ömrü 1 saat; dolunca 401.
- Alarm: kullanıcı başına sayı sınırı ve sayfalama yok; Telegram ChatId sahipliği doğrulanmıyor.
  Bot token varken ChatId'si olmayan (ve varsayılan `Telegram__ChatId` ayarlanmamış) kullanıcının
  alarmı, koşul sağlandığı her fiyat partisinde uyarı loglayarak tekrar denenir. Token yoksa log
  gönderici ChatId'ye bakmaz, alarm bir kez loglanıp pasif olur.
- Bildirimden hemen sonra alarm silinmesi ve Telegram 429 artık testlerle kapsanıyor
  (`AlertProcessorTests`, `TelegramAlertNotificationSenderTests`); canlı ortamda denenmedi.