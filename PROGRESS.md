# Proje İlerlemesi

## Tamamlananlar
- [x] Aşama 1: .NET 8 katmanlı solution iskeleti
- [x] CoinGecko sağlayıcısı ile BTC/USD ve BTC/TRY fiyatı
- [x] `GET /api/prices` endpoint'i
- [x] Swagger
- [x] `.gitignore`, `README.md`, `.env.example`

## Kalanlar
- [~] Aşama 2: EF Core, entity'ler, migration ve fiyat geçmişi kaynakları yazıldı; paket restore edilemediği için doğrulanamadı
- [ ] Aşama 3: TCMB provider, BackgroundService, cache ve retry
- [ ] Aşama 4: Kullanıcı kaydı/girişi, JWT ve takip listesi
- [ ] Aşama 5: Alarm sistemi ve Telegram bildirimi
- [ ] Aşama 6: Unit testler
- [ ] Aşama 7: Docker, docker-compose ve tamamlanmış dokümantasyon

## Test durumu
- Aşama 1 `dotnet build CryptoFxTracker.slnx --no-restore`: başarılı; 0 hata, 0 uyarı.
- Aşama 2 `dotnet restore CryptoFxTracker.slnx --disable-parallel`: başarısız; NuGet erişimi `NU1301` ile engellendi.
- Aşama 2 build/migration üretimi: test edilmedi; EF Core ve Swashbuckle paketleri indirilemedi.
- Swagger ve API canlı denemesi: başlatılamadı. Windows uygulama denetimi, `CryptoFxTracker.Application.dll` yüklenmesini `0x800711C7` ile engelledi.
- CoinGecko gerçek ağ çağrısı: test edilemedi; API süreç başlatma aşamasında durdu.
- SQL Server ve Docker: çalıştırılmadı.

## Bilinen sorunlar / varsayımlar
- CoinGecko ücretsiz API'sinin herkese açık endpoint'i `api.coingecko.com/api/v3/simple/price` kullanılacaktır.
- Ağ veya CoinGecko rate limit'i nedeniyle endpoint çağrısı başarısız olabilir; provider bu hatayı HTTP 502 ile dışarı aktarır.
- Swashbuckle paket referansı eklendi ancak NuGet erişimi engellendiği için restore edilemedi; elle yazılmış Swagger kodu kaldırıldı.
- EF Core migration dosyaları kaynak olarak eklendi; `dotnet ef` ile üretim/doğrulama yapılamadı.

## GitHub durumu
- Private repo: https://github.com/Mahanaim-Virgil/crypto-fx-tracker
- Yerel commit: Aşama 1 `876d7c8`; Aşama 2 commit'i aşağıdaki raporda belirtilecek.
- Yerel tag: `stage-1`; Aşama 2 için `stage-2` etiketi eklenecek.
- Push durumu: Başarısız. GitHub'a HTTPS bağlantısı bu ortamda `github.com:443` bağlantı hatasıyla engellendi. Repo oluşturuldu; push işlemi daha sonra ağ erişimi olan bir terminalden yapılmalıdır.
