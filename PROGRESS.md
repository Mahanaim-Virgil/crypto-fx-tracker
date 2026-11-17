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
- [x] Aşama 5: Alarm sistemi ve Telegram bildirimi (yerelde derlendi, iki migration uygulandı, uçtan uca denendi, gerçek Telegram mesajı alındı)
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

## Aşama 5 doğrulama (yerel makine)
- Önbellek düzeltmesi: API iki döngü çalıştırıldı; ikinci döngüde (TCMB satırları zaten varken) `/api/prices` BTC, USDTRY ve EURTRY'nin üçünü de döndürdü.
- Migration'lar `AddPriceAlerts` ve `AddUserTelegramChatId` `dotnet ef` ile üretildi, SQL betiği incelendi, LocalDB'ye uygulandı ve `sqlcmd` ile kontrol edildi.
- `/api/alerts` için curl betiğiyle 23 durum denendi, hepsi beklenen kodu verdi: token'sız 401; geçerli alarm 201; desteklenmeyen sembol, sayısal/bilinmeyen koşul, 0/negatif/çok büyük fiyat, 8'den fazla ondalık 400; başka kullanıcının alarmını silme 404; `GET` yanıtındaki tarihler `Z` ile.
- Alarm servisi: iki toplama döngüsü boyunca `BTC Above 1` ve `USDTRY Below 1000` birer kez çaldı ve pasif oldu, `BTC Above 999999999999` aktif kaldı.
- Telegram hata yolu (sahte token): Telegram `401 Unauthorized` döndü, alarmlar aktif kalıp her döngüde tekrar denendi; token'ın gizli kısmı ve `api.telegram.org` adresi logda 0 kez geçti. ChatId'siz kullanıcı için açıklayıcı hata loglandı.
- `Telegram__ChatId=abc` ile API açılışta `Failed to convert configuration value` hatasıyla durdu.
- Gerçek Telegram: kullanıcının kendi botuyla Swagger'dan `BTC Above 1` alarmı kuruldu, mesaj Telegram'a geldi.
- Denenmedi: alarm bildirimi gittikten hemen sonra kullanıcının alarmı silmesi (`DbUpdateConcurrencyException` yolu) ve Telegram 429 (rate limit). Aşama 6'da birim testlerle denenecek.

## Bilinen sorunlar / varsayımlar
- CoinGecko ücretsiz API'sinin herkese açık endpoint'i `api.coingecko.com/api/v3/simple/price` kullanılacaktır.
- Ağ veya CoinGecko rate limit'i nedeniyle endpoint çağrısı başarısız olabilir; provider bu hatayı HTTP 502 ile dışarı aktarır.
- Aşama 3'te entity/schema değişmediği için yeni migration gerekmedi; mevcut Aşama 2 migration'ı kullanılıyor.
- `PriceUpdate:IntervalSeconds` varsayılanı 60, retry sayısı 3, retry gecikmesi 2 saniyedir.
- Aşama 4'te `PriceUsd` nullable yapıldı ve TCMB aynı `Symbol + RecordedAtUtc` kaydını tekrar eklemiyor.
- JWT secret yalnızca `Jwt__Secret` ortam değişkeninden bekleniyor; minimum 32 karakter kontrolü var.
- `PriceHistory.PriceUsd` nullable yapıldı; TCMB kayıtlarında sabit `1` yerine `null` yazılıyor.
- `/api/prices` yanıtında USDTRY ve EURTRY için `usd` alanı `1` dönüyor; EURTRY için anlamsız (`PriceQuote.Usd` nullable değil).
- Kullanıcı başına alarm sayısı sınırı yok, `GET /api/alerts` sayfalama yapmıyor.
- `PUT /api/users/me/telegram` ChatId'nin gerçekten o kullanıcıya ait olduğunu doğrulamıyor (doğrusu `/start <kod>` ile eşleştirme).
- Bot token varken ChatId'si olmayan ve varsayılan `Telegram__ChatId` ayarlanmamış kullanıcının alarmı, koşul sağlandığı her fiyat partisinde (dakikada bir) uyarı loglayarak tekrar denenir. Bot token yoksa log gönderici ChatId'ye bakmaz; alarm bir kez loglanıp pasif olur.
- Alarmlar bildirimden sonra her biri ayrı kaydedilir; kayıt başarısız olursa aynı bildirim iki kez gidebilir (bilinçli takas).

## GitHub durumu
- Public repo: https://github.com/Mahanaim-Virgil/crypto-fx-tracker
- Her aşama `stage-N` etiketiyle işaretlenir (stage-1 ... stage-5).