# CLAUDE.md

## Proje
Kripto & Döviz Fiyat Takip ve Alarm Sistemi. .NET 8 Web API, katmanlı yapı:
`src/CryptoFxTracker.Api`, `Application`, `Domain`, `Infrastructure`.
EF Core + MSSQL LocalDB, CoinGecko (`IPriceProvider`) ve TCMB (`ITcmbProvider`)
sağlayıcıları, `PriceCollectorService` (BackgroundService, 60 sn), `IMemoryCache`,
Swashbuckle, JWT kimlik doğrulama (`PasswordHasher<User>`).

## Aşamalar
- Tamamlandı (GitHub'da `stage-1` ... `stage-4` etiketleri): 1 iskelet + CoinGecko,
  2 EF Core + TCMB, 3 arka plan toplayıcı + cache + retry, 4 kayıt/giriş + JWT + takip listesi.
- Sıradaki: 5 alarm + Telegram, 6 xUnit testleri, 7 Docker + README + Mimari Kararlar.
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
  Engel aralıklı, `dotnet build` sonrası tekrar dene.
- Swagger: `http://localhost:5000/swagger`.

## Komutlar
- Derle: `dotnet build` (kök klasörde).
- `Jwt__Secret` ortam değişkeni (en az 32 karakter) yoksa API açılmaz ve `dotnet ef` de
  hata verir (`dotnet ef` uygulamayı başlatır). Her yeni cmd penceresinde `set Jwt__Secret=...`
  gerekir. Gizli anahtarı koda, repoya veya belgeye yazma.
- Migration ekle: `dotnet ef migrations add ADI --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Api --output-dir Persistence/Migrations`
- Migration uygula: `dotnet ef database update --project src/CryptoFxTracker.Infrastructure --startup-project src/CryptoFxTracker.Api`
- Sorgu: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CryptoFxTracker -C -Q "..."`
- Migration `dotnet ef` ile üretilir, elle yazılmaz. Migration dosyaları ve
  `CryptoFxTrackerDbContextModelSnapshot.cs` birlikte commit edilir.

## Veritabanı notları
- Tablolar: `PriceHistories` (Id, Symbol, Provider, PriceUsd nullable, PriceTry, RecordedAtUtc),
  `Users` (e-posta unique), `UserWatchlists` (birleşik anahtar UserId + Symbol), `__EFMigrationsHistory`.
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
- Yapılacak: ilk 7 commit eski, yanlış imzalı (biri gerçek e-postayı içeriyor). Repo public
  yapılmadan önce geçmiş yedek alınarak düzeltilecek.

## Bilinen eksikler
- Girdi doğrulaması (e-posta biçimi, şifre uzunluğu) test edilmedi.
- Otomatik test yok (Aşama 6).
- Takip listesine aynı sembol ikinci kez eklenince 409 değil 200 dönüyor (idempotent, bilinçli).
- Swagger'da 401, 409, 204 yanıtları belgelenmemiş.
- JWT ömrü 1 saat; dolunca 401.