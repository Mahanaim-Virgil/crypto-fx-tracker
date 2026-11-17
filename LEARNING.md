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

## Aşama 5 — Fiyat alarmları ve Telegram bildirimi

### Kavramlar

1. **Channel (üretici/tüketici kuyruğu)**: `PriceCollectorService` her fiyat partisini `System.Threading.Channels` ile bir kuyruğa bırakır, `AlertCheckerService` oradan okur. Kapasite 1 ve `DropOldest` seçildi: toplayıcı hiç beklemez, alarm servisi geride kalırsa yalnızca en güncel fiyat işlenir. Mülakatta, iki işin birbirinin hızına bağlanmaması (decoupling) ve geri basınç (backpressure) stratejisi olarak anlatılabilir.
2. **İş kuralının entity'de olması**: "Alarm bu fiyatta çalar mı?" kuralı `PriceAlert.IsTriggeredBy` içinde. Veritabanı, ağ veya DI olmadan test edilebilir ve tek yerde durur. Takas: bir sembolün tüm aktif alarmları belleğe okunur.
3. **En az bir kez (at-least-once) teslim**: Alarm, bildirim gittikten sonra ve her biri ayrı kaydedilerek pasif yapılır. Kayıt başarısız olursa bildirim tekrar gidebilir; bildirimin hiç gitmemesi daha kötü olduğu için bu takas kabul edildi.
4. **Arayüz arkasında dış servis**: `IAlertNotificationSender` sayesinde bot token yoksa loga yazan gönderici, varsa Telegram göndericisi kullanılır. Telegram kurmadan da akış uçtan uca çalışır ve Aşama 6'da sahte (fake) gönderici ile test edilebilir.
5. **Gizli bilgiyi loglardan uzak tutmak**: Telegram'da bot token istek adresinin içinde. `IHttpClientFactory` varsayılan olarak her isteğin adresini loglar; Telegram istemcisinde bu loglar `RemoveAllLoggers()` ile kapatıldı.

### Karşılaşılan hatalar ve nedenleri

- **Önbellekten düşen kurlar**: TCMB kuru veritabanında zaten varsa `continue` ile atlanıyor, bu da önbelleğe eklenmeden önce oluyordu. Günün ilk döngüsünden sonra `/api/prices` yalnızca BTC döndürüyordu. Kur artık veritabanı kontrolünden önce önbelleğe ekleniyor.
- **Sessizce yuvarlanan hedef fiyat**: Kolon `decimal(20,8)`; `0.000000001` doğrulamayı geçip veritabanına 0 olarak yazılıyordu. 8'den fazla ondalık basamak artık 400 ile reddediliyor.
- **"Z"siz tarih**: EF Core `datetime2` okurken `DateTimeKind.Unspecified` döndürüyor; JSON'da `Z` eksik kalıyordu. Yanıtta tarih UTC olarak işaretleniyor.
- **Mutlak sayılan Telegram adresi**: `bot123456:ABC.../sendMessage`, .NET'te `bot123456:` şemalı mutlak adres sayılıyor ve `BaseAddress` yok sayılıyordu; gerçek token'la hiçbir mesaj gitmezdi. Adresin başına `/` eklendi. Bu hata kod çalıştırılmadan, bağımsız kod incelemesinde bulundu.
- **Windows Akıllı Uygulama Denetimi**: Yeniden derlenen dll'ler engellendi (`0x800711C7`). Deterministik derleme her seferinde aynı dosyayı ürettiği için tekrar denemek işe yaramadı; `dotnet build --no-incremental -p:Deterministic=false` ile her derlemede farklı dosya üretilerek aşıldı.

### Kontrol soruları

1. Kuyruk kapasitesi 1 ve `DropOldest` olduğunda alarm servisi yavaşlarsa ne olur?
2. Alarm neden bildirim gönderilmeden önce değil, gönderildikten sonra pasif yapılıyor?
3. Bot token'ı istek adresinde taşıyan bir API kullanırken nelere dikkat edilmeli?

## Cevaplar

1. Toplayıcı beklemez; kuyruktaki eski parti atılır ve alarm servisi sıradaki okumada en güncel fiyatı görür. Ara fiyatlar kontrol edilmez, bu kabul edilen bir takastır.
2. Önce pasif yapılsaydı ve gönderim başarısız olsaydı, kullanıcı hiç haber almazdı. Sonra pasif yapıldığı için en kötü durumda aynı bildirim iki kez gider.
3. İstek adresini yazan HTTP logları kapatılmalı, hata mesajlarına adres yazılmamalı ve token yalnızca ortam değişkeninden okunmalı. Token sızarsa (örneğin bir ekran görüntüsünde) BotFather'da `/revoke` ile hemen yenilenmeli.

## Aşama 6 — xUnit testleri ve GitHub Actions

### Kavramlar

1. **Birim testi ve entegrasyon testi**: Birim testleri (`UnitTests`) ağ ve veritabanı olmadan tek bir sınıfı sınar; hızlıdır ve kırıldığında sorunun yeri bellidir. Entegrasyon testleri (`IntegrationTests`) parçaları birlikte, gerçek bir veritabanıyla ve HTTP üzerinden sınar. İki ayrı proje, bu iki tür testi karıştırmamak için.
2. **Bellekte SQLite ve EF InMemory farkı**: EF InMemory ilişkisel değildir; silinmiş bir satırı güncellemenin `DbUpdateConcurrencyException` fırlatması gibi davranışları gerçekçi göstermez. Bellekte SQLite gerçek bir ilişkisel veritabanıdır ve kurulum gerektirmez. Takas: SQL Server değildir; `DateTimeOffset` sıralaması ve `decimal(20,8)` yuvarlaması gibi farklar testlerde görünmez.
3. **`WebApplicationFactory`**: Gerçek API'yi (`Program.cs` ile) bellekte açar ve gerçek HTTP istekleri atılır. Testte veritabanı SQLite ile değiştirildi, arka plan servisleri kaldırıldı, JWT anahtarı `UseSetting` ile rastgele verildi.
4. **Elle yazılmış sahte nesneler (fake)**: Moq gibi bir kütüphane yerine `FakeAlertNotificationSender` ve isteği kaydeden bir `HttpMessageHandler`. Sahte sınıf okununca ne yaptığı bellidir; testler gerçek ağa çıkmaz.
5. **Test edilebilir tasarım**: Alarm işleme `private` bir metottaydı; test etmek için arka plan servisini çalıştırıp beklemek gerekirdi, bu da ara sıra kendiliğinden kırılan testler demektir. Mantık `public` bir `AlertProcessor` sınıfına taşındı; davranış değişmedi, testler doğrudan çağırıyor.
6. **Sürekli entegrasyon (CI)**: GitHub Actions her push'ta derleme ve testleri temiz bir Linux makinesinde çalıştırır. "Bende çalışıyor" yerine herkesin görebileceği bir sonuç üretir.

### Karşılaşılan sorunlar ve nedenleri

- **Windows testleri engelledi**: Akıllı Uygulama Denetimi yeni derlenen test dll'lerini engelledi. Dosya konumu ve adı değiştirilerek denendi, sonuç değişmedi; kontrol olarak daha önce geçen bir deneme projesi de artık engellendi. Yani karar dosyaya değil zamana veya makine durumuna bağlıydı. Testler CI'a taşındı.
- **Kilitli dll'ler**: `MSB3021` "dosya başka bir süreç tarafından kullanılıyor" hatası, arka planda açık kalan API'den kaynaklandı. Bu, Windows engelinden ayrı bir sorundu; API kapatılınca çözüldü.
- **Yarış durumu**: Toplayıcı fiyat partisini kaydetmeden önce kuyruğa koyuyor. Test ikinci partiyi okuyup hemen durdursaydı ikinci kayıt yarıda iptal olabilir, test ara sıra kırılırdı. Durdurmadan önce üçüncü parti beklenerek düzeltildi. Bu sorun bağımsız inceleme sırasında bulundu.
- **Eksik regresyon koruması**: `triggeredAtUtc` için UTC kontrolü yoktu; o satırdaki düzeltme geri alınsa hiçbir test kırılmazdı. Bu da incelemede bulundu.

### Kontrol soruları

1. Testlerde neden EF InMemory değil SQLite kullanıldı?
2. Toplayıcı testi neden `Task.Delay` ile beklemek yerine kuyruktan parti okuyor?
3. Bir regresyon testinin gerçekten işe yaradığından nasıl emin olunur?

## Cevaplar

1. Bildirim sırasında silinen alarm senaryosu, silinmiş satırı güncellemenin gerçekten 0 satır etkileyip `DbUpdateConcurrencyException` fırlatmasına dayanıyor. Bunu ilişkisel bir veritabanı gerçekçi biçimde gösterir; InMemory göstermez.
2. Süreye dayalı bekleme yavaş makinede yetmeyebilir, hızlı makinede boşa zaman harcar. Kuyruktan bir parti okumak, o döngünün o noktaya geldiğini kesin olarak gösterir.
3. Düzeltilen hata geçici olarak geri getirilir ve testin kırıldığı görülür (mutation testing). Bu projede inceleme kodu okuyarak yapıldı; hatayı geri getirip CI'da çalıştırmak bir sonraki doğrulama adımı olabilir.
