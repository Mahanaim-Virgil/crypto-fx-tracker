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
- [x] Aşama 4: Kullanıcı kaydı/girişi, JWT ve takip listesi (yerelde derlendi, migration uygulandı, Swagger'dan test edildi)
- [ ] Aşama 5: Alarm sistemi ve Telegram bildirimi
- [ ] Aşama 6: Unit testler
- [ ] Aşama 7: Docker, docker-compose ve tamamlanmış dokümantasyon

## Test durumu
- Aşama 1 `dotnet build CryptoFxTracker.slnx --no-restore`: başarılı; 0 hata, 0 uyarı.
## Doğrulama (yerel makine)
- `dotnet restore` ve `dotnet build`: başarılı (Domain, Application, Infrastructure, Api).
- Migration `AddUsersWatchlistAndNullablePriceUsd` `dotnet ef migrations add` ile üretildi ve LocalDB'ye uygulandı.
- Swagger üzerinden test edildi: kayıt (aynı e-posta ikinci kez 409), giriş (JWT), token'sız `GET /api/watchlist` 401, token'lı sembol ekleme/listeleme/silme.
- TCMB satırları artık tekrar yazılmıyor, TCMB için `PriceUsd` NULL, `GET /api/price-history` en yeniden eskiye sıralı.
- Canlı deneme: API `dotnet <dll> --urls http://localhost:5000` ile çalıştırıldı, Swagger arayüzü açıldı.
- CoinGecko: fiyat toplayıcı dakikada bir BTC satırı yazıyor (`PriceHistories` tablosunda doğrulandı).
- Docker: henüz denenmedi (Aşama 7).

## Bilinen sorunlar / varsayımlar
- CoinGecko ücretsiz API'sinin herkese açık endpoint'i `api.coingecko.com/api/v3/simple/price` kullanılacaktır.
- Ağ veya CoinGecko rate limit'i nedeniyle endpoint çağrısı başarısız olabilir; provider bu hatayı HTTP 502 ile dışarı aktarır.
- Aşama 3'te entity/schema değişmediği için yeni migration gerekmedi; mevcut Aşama 2 migration'ı kullanılıyor.
- `PriceUpdate:IntervalSeconds` varsayılanı 60, retry sayısı 3, retry gecikmesi 2 saniyedir.
- Aşama 4'te `PriceUsd` nullable yapıldı ve TCMB aynı `Symbol + RecordedAtUtc` kaydını tekrar eklemiyor.
- JWT secret yalnızca `Jwt__Secret` ortam değişkeninden bekleniyor; minimum 32 karakter kontrolü var.
- `PriceHistory.PriceUsd` nullable yapıldı; TCMB kayıtlarında sabit `1` yerine `null` yazılıyor.

## GitHub durumu
- Private repo: https://github.com/Mahanaim-Virgil/crypto-fx-tracker
- Her aşama `stage-N` etiketiyle işaretlenir (stage-1, stage-2, stage-3, stage-4).