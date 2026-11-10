# Proje İlerlemesi

## Tamamlananlar
- [x] Aşama 1: .NET 8 katmanlı solution iskeleti
- [x] CoinGecko sağlayıcısı ile BTC/USD ve BTC/TRY fiyatı
- [x] `GET /api/prices` endpoint'i
- [x] Swagger
- [x] `.gitignore`, `README.md`, `.env.example`

## Kalanlar
- [x] Aşama 2: EF Core, entity'ler, migration ve fiyat geçmişi
- [~] Aşama 3: BackgroundService, cache, retry ve fiyat kaydı kaynakları yazıldı; canlı doğrulama yapılamadı
- [~] Aşama 4: Kullanıcı kaydı/girişi, JWT ve takip listesi kaynakları yazıldı; migration/build doğrulaması yapılamadı
- [ ] Aşama 5: Alarm sistemi ve Telegram bildirimi
- [ ] Aşama 6: Unit testler
- [ ] Aşama 7: Docker, docker-compose ve tamamlanmış dokümantasyon

## Test durumu
- Aşama 1 `dotnet build CryptoFxTracker.slnx --no-restore`: başarılı; 0 hata, 0 uyarı.
- Aşama 2 `dotnet restore CryptoFxTracker.slnx --disable-parallel`: başarısız; NuGet erişimi `NU1301` ile engellendi.
- Aşama 2 build/migration üretimi: test edilmedi; EF Core ve Swashbuckle paketleri indirilemedi.
- Aşama 3 `dotnet build`: başarısız; API ve Infrastructure projelerinde NuGet `NU1301`.
- Aşama 3 `dotnet build --no-restore`: başarısız; mevcut assets restore kaynağı hatasını yine raporladı.
- Aşama 4 `dotnet ef migrations add AddUsersWatchlistAndNullablePriceUsd`: başarısız; `dotnet-ef` aracı kurulu değil. Elle migration yazılmadı.
- Aşama 4 `dotnet restore`: başarısız; API ve Infrastructure projelerinde NuGet `NU1301`.
- Aşama 4 `dotnet build --no-restore`: başarısız; aynı `NU1301` hatası devam etti. Application ve Domain derlendi, API ve Infrastructure derlenemedi.
- Aşama 4 API, SQL Server ve endpoint testi: çalıştırılmadı; build/restore engeli nedeniyle doğrulanamadı.
- API çalıştırma, birkaç BackgroundService döngüsü, `sqlcmd` ile PriceHistories doğrulaması ve `GET /api/price-history`: test edilmedi; build/restore engeli nedeniyle çalıştırılamadı.
- Swagger ve API canlı denemesi: başlatılamadı. Windows uygulama denetimi, `CryptoFxTracker.Application.dll` yüklenmesini `0x800711C7` ile engelledi.
- CoinGecko gerçek ağ çağrısı: test edilemedi; API süreç başlatma aşamasında durdu.
- SQL Server ve Docker: çalıştırılmadı.

## Bilinen sorunlar / varsayımlar
- CoinGecko ücretsiz API'sinin herkese açık endpoint'i `api.coingecko.com/api/v3/simple/price` kullanılacaktır.
- Ağ veya CoinGecko rate limit'i nedeniyle endpoint çağrısı başarısız olabilir; provider bu hatayı HTTP 502 ile dışarı aktarır.
- Swashbuckle paket referansı eklendi ancak NuGet erişimi engellendiği için restore edilemedi; elle yazılmış Swagger kodu kaldırıldı.
- EF Core migration dosyaları kaynak olarak eklendi; `dotnet ef` ile üretim/doğrulama yapılamadı.
- Aşama 3'te entity/schema değişmediği için yeni migration gerekmedi; mevcut Aşama 2 migration'ı kullanılıyor.
- `PriceUpdate:IntervalSeconds` varsayılanı 60, retry sayısı 3, retry gecikmesi 2 saniyedir.
- Aşama 4'te `PriceUsd` nullable yapıldı ve TCMB aynı `Symbol + RecordedAtUtc` kaydını tekrar eklemiyor.
- JWT secret yalnızca `Jwt__Secret` ortam değişkeninden bekleniyor; minimum 32 karakter kontrolü var.
- `PriceHistory.PriceUsd` nullable yapıldı; TCMB kayıtlarında sabit `1` yerine `null` yazılıyor.

## GitHub durumu
- Private repo: https://github.com/Mahanaim-Virgil/crypto-fx-tracker
- Aşama 3 yerel commit: `feat: add background price collection and cache`.
- Aşama 3 yerel tag: `stage-3`.
- Push denemeleri: `git push` ve `git push origin stage-3` birer kez denendi; ikisi de `github.com:443` bağlantı hatasıyla başarısız oldu. Tekrar denenmedi.
- Push durumu: Başarısız. GitHub'a HTTPS bağlantısı bu ortamda `github.com:443` bağlantı hatasıyla engellendi. Repo oluşturuldu; push işlemi daha sonra ağ erişimi olan bir terminalden yapılmalıdır.
