# CLAUDE.md

## Proje
Kripto & Döviz Fiyat Takip ve Alarm Sistemi. .NET 8 Web API, katmanlı yapı:
`src/CryptoFxTracker.Api`, `Application`, `Domain`, `Infrastructure`.
EF Core + MSSQL LocalDB, CoinGecko (`IPriceProvider`) ve TCMB (`ITcmbProvider`)
sağlayıcıları, `PriceCollectorService` (BackgroundService, 60 sn), `IMemoryCache`,
Swashbuckle, JWT kimlik doğrulama (`PasswordHasher<User>`).

## Aşamalar
- Tamamlandı (`stage-1` ... `stage-5` etiketleri): 1 iskelet + CoinGecko,
  2 EF Core + TCMB, 3 arka plan toplayıcı + cache + retry, 4 kayıt/giriş + JWT + takip listesi,
  5 fiyat alarmı (`Channel` + `AlertCheckerService`) + Telegram bildirimi.
- Sıradaki: 6 xUnit testleri, 7 Docker + README + Mimari Kararlar.
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

## Komutlar
- Derle: `dotnet build` (kök klasörde).
- `Jwt__Secret` ortam değişkeni (en az 32 karakter) yoksa API açılmaz ve `dotnet ef` de
  hata verir (`dotnet ef` uygulamayı başlatır). Her yeni cmd penceresinde `set Jwt__Secret=...`
  gerekir. Gizli anahtarı koda, repoya veya belgeye yazma.
- Telegram isteğe bağlı: `Telegram__BotToken` (yoksa bildirimler yalnızca loga gider) ve
  `Telegram__ChatId` (varsayılan sohbet, sayı olmalı; değilse API açılmaz). Token istek
  adresinde olduğu için Telegram HttpClient'ının logları kapalı; bunu geri açma. Token ekran
  görüntüsüne girerse BotFather'da `/revoke` ile yenilenmeli.
- Migration ekle: `dotnet ef migrations add ADI --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Api --output-dir Persistence/Migrations`
- Migration uygula: `dotnet ef database update --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Api`
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
- Commit mesajı `feat:`, `fix:`, `docs:` önekiyle; küçük ve tek konulu commit.
- Push etmeden önce sor. Geçmiş yeniden yazma (force push) ayrıca onay ister.
- Git kimliği global ayardan gelir (GitHub noreply adresi). Repoya yerel `user.name` veya
  `user.email` yazma.
- Geçmiş 2026-11-16'da `filter-branch` ile düzeltildi (tüm commit ve tag'ler noreply adresinde),
  force push edildi ve repo public yapıldı. Yedek: Masaüstünde `crypto-fx-tracker-backup-2026-11-16.*`.

## Bilinen eksikler
- Girdi doğrulaması (e-posta biçimi, şifre uzunluğu) test edilmedi.
- Otomatik test yok (Aşama 6).
- Takip listesine aynı sembol ikinci kez eklenince 409 değil 200 dönüyor (idempotent, bilinçli).
- Swagger'da eski endpoint'lerin (auth, watchlist) 401, 409, 204 yanıtları belgelenmemiş;
  `/api/alerts` ve `/api/users/me/telegram` belgelendi.
- JWT ömrü 1 saat; dolunca 401.
- Alarm: kullanıcı başına sayı sınırı ve sayfalama yok; Telegram ChatId sahipliği doğrulanmıyor.
  Bot token varken ChatId'si olmayan (ve varsayılan `Telegram__ChatId` ayarlanmamış) kullanıcının
  alarmı, koşul sağlandığı her fiyat partisinde uyarı loglayarak tekrar denenir. Token yoksa log
  gönderici ChatId'ye bakmaz, alarm bir kez loglanıp pasif olur.
- Denenmedi: bildirimden hemen sonra alarm silinmesi (`DbUpdateConcurrencyException` yolu), Telegram 429.