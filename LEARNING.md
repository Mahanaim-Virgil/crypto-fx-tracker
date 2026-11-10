# Öğrenme Notları

## Aşama 3 — BackgroundService, cache ve retry

### Kavramlar

1. **BackgroundService**: HTTP isteği beklemeden arka planda çalışan hosted service sınıfıdır. Bu projede belirli aralıklarla fiyat toplayıp veritabanına kaydetmek için kullanıldı. Mülakatta, uygulama başlarken başlayan ve cancellation token ile güvenli kapanan periyodik iş olarak anlatılabilir.
2. **Scoped servis ve scope oluşturma**: `DbContext` scoped olduğu için singleton ömürlü BackgroundService içinde doğrudan tutulmaz. Her döngüde yeni scope oluşturulur. Mülakatta, uzun yaşayan servislerin kısa ömürlü bağımlılıkları `IServiceScopeFactory` ile kullanması gerektiği söylenebilir.
3. **IMemoryCache**: Sık okunan güncel veriyi RAM’de tutar. `/api/prices` her istekte dış servise gitmek yerine son başarılı toplama sonucunu cache’ten okur. Uygulama yeniden başlarsa cache temizlenir.
4. **Retry ve hata izolasyonu**: Geçici ağ hatalarında aynı kaynak sınırlı sayıda tekrar denenir. CoinGecko başarısız olsa bile TCMB ve sonraki döngü çalışmaya devam eder. Mülakatta retry sayısı ve gecikmesinin sınırsız olmaması gerektiği vurgulanabilir.
5. **CancellationToken**: Uygulama kapanırken bekleyen HTTP, delay ve EF işlemlerinin kontrollü iptal edilmesini sağlar.

### Karşılaşılan hatalar ve nedenleri

- Önceki klasörde çalışılan Aşama 2 bu repoda yoktu; kullanıcı doğru `crypto-fx-tracker` klasörünü seçtikten sonra mevcut Aşama 2 commit'i doğrulandı.
- Bu aşamada yeni entity veya tablo eklenmediği için migration gerekmedi. `PriceHistories` tablosu Aşama 2 migration'ı ile kullanılıyor.

### Kontrol soruları

1. BackgroundService içinde `CryptoFxTrackerDbContext` neden constructor'da doğrudan tutulmamalıdır?
2. CoinGecko üç retry sonrasında da başarısız olursa TCMB ve sonraki toplama döngüsüne ne olur?

## Cevaplar

1. BackgroundService uzun ömürlü/singleton, DbContext ise scoped olduğu için doğrudan enjekte edilirse yaşam döngüsü uyuşmazlığı ve thread-safety sorunları oluşabilir. Her döngüde scope oluşturulmalıdır.
2. CoinGecko hatası loglanır. TCMB ayrı try/catch bloğunda çalışır ve servis çökmeksizin sonraki döngüye geçer.

## Aşama 4 — Kimlik doğrulama ve takip listesi

### Kavramlar

1. **Password hashing**: Parola düz metin olarak saklanmaz. `PasswordHasher<User>` tek yönlü hash üretir ve girişte doğrulama yapar. Mülakatta, veritabanı sızsa bile düz parolaların tutulmadığı anlatılır.
2. **JWT Bearer authentication**: Girişten sonra sunucu imzalı bir token üretir. İstemci sonraki isteklerde `Authorization: Bearer <token>` gönderir. API token imzasını, issuer, audience ve süresini doğrular.
3. **Claims**: JWT içindeki kullanıcı kimliği ve e-posta gibi bilgiler endpoint'lerde `ClaimsPrincipal` üzerinden okunur. Takip listesi sorgusu kullanıcı ID claim'ine göre filtrelenir.
4. **Composite key ve unique index**: Takip listesinde `{UserId, Symbol}` birleşik anahtardır. Aynı kullanıcı aynı sembolü iki kez ekleyemez; e-posta için unique index kullanılır.
5. **Nullable veri modeli**: TCMB satırlarında USD karşılığı olmadığı için `PriceUsd` nullable yapıldı. Anlamlı olmayan bir değeri `1` gibi sahte bir değerle doldurmak yerine `null` saklanır.

### Karşılaşılan hatalar ve nedenleri

- `dotnet ef migrations add AddUsersWatchlistAndNullablePriceUsd` denendi ancak `dotnet-ef` aracı bu ortamda kurulu değildi. Talimat gereği migration elle yazılmadı.
- Yeni JWT ve Identity paketleri proje dosyalarına eklendi; restore/build sonucu paket erişimine bağlıdır.

### Kontrol soruları

1. JWT secret neden kaynak koduna veya gerçek `appsettings.json` dosyasına yazılmamalıdır?
2. Takip listesinde `{UserId, Symbol}` birleşik anahtarı neyi engeller?

## Cevaplar

1. Kaynak kodu veya repository sızarsa token üretme anahtarı da sızar. Secret ortam değişkeni veya güvenli secret store üzerinden verilmelidir.
2. Aynı kullanıcının aynı sembolü birden fazla kez takip listesine eklemesini engeller.
