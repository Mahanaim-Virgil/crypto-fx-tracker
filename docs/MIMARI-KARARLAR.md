# Mimari Kararlar

Bu belge, projenin geliştirilmesi sırasında alınan önemli kararları ve nedenlerini toplar. Her karar
aynı biçimde yazıldı: **bağlam** (hangi sorun vardı), **seçenekler**, **karar** ve **takas** (neyi göze
aldık). Takaslar bilinçli seçimlerdir; ilerideki bir ihtiyaç kararı değiştirebilir.

İçindekiler

1. [Katmanlı yapı](#1-katmanlı-yapı)
2. [Minimal API](#2-minimal-api)
3. [EF Core ve SQL Server](#3-ef-core-ve-sql-server)
4. [Fiyat toplama: BackgroundService ve scope](#4-fiyat-toplama-backgroundservice-ve-scope)
5. [Güncel fiyatlar için IMemoryCache](#5-güncel-fiyatlar-için-imemorycache)
6. [Dış servis hataları: sınırlı yeniden deneme ve hata izolasyonu](#6-dış-servis-hataları-sınırlı-yeniden-deneme-ve-hata-izolasyonu)
7. [TCMB kurlarında mükerrer kayıt kontrolü](#7-tcmb-kurlarında-mükerrer-kayıt-kontrolü)
8. [Kimlik doğrulama: JWT ve PasswordHasher](#8-kimlik-doğrulama-jwt-ve-passwordhasher)
9. [Gizli bilgiler ortam değişkenlerinde](#9-gizli-bilgiler-ortam-değişkenlerinde)
10. [Toplayıcı ile alarm kontrolü arasında Channel](#10-toplayıcı-ile-alarm-kontrolü-arasında-channel)
11. [Alarm kuralı entity'de](#11-alarm-kuralı-entityde)
12. [Tek seferlik alarm ve en az bir kez teslim](#12-tek-seferlik-alarm-ve-en-az-bir-kez-teslim)
13. [Bildirim arayüzü ve Telegram](#13-bildirim-arayüzü-ve-telegram)
14. [API girdi doğrulaması](#14-api-girdi-doğrulaması)
15. [Test stratejisi](#15-test-stratejisi)
16. [Sürekli entegrasyon: GitHub Actions](#16-sürekli-entegrasyon-github-actions)
17. [Docker ve docker compose](#17-docker-ve-docker-compose)
18. [Migration'ların uygulanması](#18-migrationların-uygulanması)

---

## 1. Katmanlı yapı

**Bağlam:** HTTP, iş kuralları, veritabanı ve dış servis kodu birbirine karışırsa değiştirmek ve test etmek zorlaşır.

**Seçenekler:** Tek proje; dört katman (Api, Application, Domain, Infrastructure); özellik bazlı dilimler (vertical slices).

**Karar:** Dört katman. `Domain` entity'ler ve iş kuralları (ör. `PriceAlert.IsTriggeredBy`), `Application`
arayüzler ve modeller (`IPriceProvider`, `IAlertNotificationSender`), `Infrastructure` EF Core, dış istemciler ve
arka plan servisleri, `Api` HTTP uç noktaları ve başlangıç.

**Takas:** Küçük bir proje için fazladan dosya ve proje referansı. Karşılığında dış servisler arayüzlerin
arkasında kaldı; testlerde sahte gönderici ve sahte sağlayıcı kullanmak bu sayede kolaylaştı.

## 2. Minimal API

**Bağlam:** Az sayıda, basit uç nokta.

**Seçenekler:** MVC controller'lar; Minimal API.

**Karar:** Minimal API. Büyüyen gruplar `Api/Endpoints` altında `MapGroup` ile ayrı dosyalara taşındı
(`AlertEndpoints`, `UserEndpoints`).

**Takas:** Auth ve watchlist uç noktaları hâlâ `Program.cs` içinde; tutarlılık için ileride onlar da taşınabilir.
Controller'lardaki otomatik model doğrulaması yok; doğrulama uç noktalarda elle yazıldı (bkz. 14).

## 3. EF Core ve SQL Server

**Bağlam:** Fiyat geçmişi, kullanıcılar, takip listesi ve alarmlar ilişkisel veri.

**Seçenekler:** EF Core; Dapper ile elle SQL; SQL Server, PostgreSQL veya SQLite.

**Karar:** EF Core ve SQL Server. Geliştirmede LocalDB, Docker'da SQL Server 2022 container'ı. Şema yalnızca
`dotnet ef` ile üretilen migration'larla değişir; migration'lar elle yazılmaz.

**Takas:** LocalDB yalnızca Windows'ta var; bu yüzden Docker'da SQL Server container'ı kullanılıyor.
PostgreSQL daha hafif olurdu ama EF sağlayıcısının ve migration'ların değişmesi gerekirdi.

## 4. Fiyat toplama: BackgroundService ve scope

**Bağlam:** Fiyatlar her istekte dış servislerden çekilirse yanıtlar yavaşlar ve servislerin istek sınırına takılınır.

**Seçenekler:** İstek anında çekmek; zamanlanmış bir iş kütüphanesi (Hangfire, Quartz); `BackgroundService`.

**Karar:** `PriceCollectorService` (`BackgroundService`) dakikada bir CoinGecko ve TCMB'den fiyat toplar.
Servis singleton, `DbContext` scoped olduğu için `DbContext` constructor'dan verilmez; her döngüde
`IServiceScopeFactory` ile yeni bir scope açılır.

**Takas:** Tek bir API örneği varsayılıyor. Birden fazla örnek çalışsaydı her biri ayrı toplar ve aynı fiyatı
birden fazla kez yazardı; o durumda toplama ayrı bir worker'a taşınmalı.

## 5. Güncel fiyatlar için IMemoryCache

**Bağlam:** `GET /api/prices` dış servise gitmeden hızlı yanıt vermeli.

**Seçenekler:** Her istekte veritabanından son fiyatları sorgulamak; `IMemoryCache`; Redis gibi dağıtık önbellek.

**Karar:** `IMemoryCache`. Toplayıcı her döngüde güncel fiyatları önbelleğe yazar. Önbellek boşsa (ilk döngü
henüz bitmemişse) uç nokta 503 döner. Fiyatlar veritabanına yazılmadan önce önbelleğe konur; veritabanına
ulaşılamasa bile güncel fiyatlar sunulur.

**Takas:** Önbellek uygulama yeniden başlayınca boşalır ve örnekler arasında paylaşılmaz. Kayıtlara son kullanma
süresi verilmedi: dış servisler uzun süre yanıt vermezse eski fiyat sunulmaya devam eder. Her fiyatta
`retrievedAtUtc` olduğu için istemci fiyatın yaşını görebilir.

## 6. Dış servis hataları: sınırlı yeniden deneme ve hata izolasyonu

**Bağlam:** CoinGecko veya TCMB geçici olarak hata verebilir.

**Seçenekler:** Hiç yeniden denememek; Polly gibi bir kütüphane; elle yazılmış basit yeniden deneme.

**Karar:** Elle yazılmış `ExecuteWithRetryAsync`: sınırlı sayıda deneme (varsayılan 3) ve sabit bekleme. CoinGecko
ve TCMB ayrı `try/catch` bloklarında; biri başarısız olursa diğeri ve sonraki döngü devam eder.

**Takas:** Üstel bekleme veya devre kesici (circuit breaker) yok. Polly bunları sağlardı ama bu ölçekte
öğretici bir el yazımı çözüm yeterli görüldü.

## 7. TCMB kurlarında mükerrer kayıt kontrolü

**Bağlam:** TCMB günde bir kur yayınlar ama toplayıcı dakikada bir çalışır; her döngüde aynı kur tekrar gelir.

**Seçenekler:** `Symbol + RecordedAtUtc` için benzersiz indeks; uygulama katmanında "var mı?" kontrolü.

**Karar:** Uygulama katmanında kontrol. `RecordedAtUtc` kurun yayın tarihidir; aynı sembol ve tarih varsa yeni
satır yazılmaz. İndeks var ama benzersiz değil.

**Takas:** İki toplayıcı aynı anda çalışırsa iki satır yazılabilir (yarış durumu). Tek örnek varsayımıyla kabul
edildi (bkz. 4). Benzersiz indeks daha sağlam olurdu.

## 8. Kimlik doğrulama: JWT ve PasswordHasher

**Bağlam:** Takip listesi ve alarmlar kullanıcıya özel.

**Seçenekler:** ASP.NET Core Identity'nin tamamı; harici kimlik sağlayıcı (OAuth); kendi kayıt/giriş akışı ve JWT.

**Karar:** Kendi kayıt/giriş akışı. Şifreler `PasswordHasher<User>` ile tek yönlü hash'lenir; girişte HMAC-SHA256
ile imzalanmış, 60 dakika geçerli bir JWT döner. Kullanıcı kimliği token'daki claim'den okunur.

**Takas:** Yenileme token'ı (refresh token), şifre sıfırlama ve e-posta doğrulama yok; token süresi dolunca
yeniden giriş gerekir. Identity bunları hazır sağlardı ama bu projenin kapsamı dışında kaldı.

## 9. Gizli bilgiler ortam değişkenlerinde

**Bağlam:** Repo public. JWT anahtarı, veritabanı şifresi ve Telegram bot token'ı repoya girmemeli.

**Seçenekler:** `appsettings.json`; .NET User Secrets; ortam değişkenleri; bir gizli bilgi kasası (Key Vault).

**Karar:** Ortam değişkenleri (`Jwt__Secret`, `Telegram__BotToken`, Docker'da `.env` dosyası). `.env` git'e girmez;
`.env.example` gereken değişkenleri boş değerlerle belgeler. JWT anahtarı yoksa veya 32 karakterden kısaysa
uygulama açılmaz; `Telegram:ChatId` sayı değilse de açılışta durur.

**Takas:** Ortam değişkenleri işlem içinde düz metin olarak durur. Üretim ortamında bir gizli bilgi kasası daha
doğru olur.

## 10. Toplayıcı ile alarm kontrolü arasında Channel

**Bağlam:** Her yeni fiyatta alarmlar kontrol edilmeli. Bildirim (Telegram) yavaşlarsa fiyat toplama etkilenmemeli.

**Seçenekler:** Alarm kontrolünü toplayıcının içinde yapmak; ayrı bir servisin önbelleği belli aralıklarla okuması;
toplayıcı ile alarm servisi arasında bir kuyruk (`System.Threading.Channels`).

**Karar:** Kuyruk. `PriceUpdateChannel` kapasitesi 1 ve `DropOldest` ile kurulu: toplayıcı hiçbir zaman beklemez,
`AlertCheckerService` kendi hızında okur ve geride kalırsa yalnızca en güncel fiyatı görür.

**Takas:** Ara fiyatlar kontrol edilmeyebilir; kısa süreli bir dalgalanma kaçabilir. Kuyruk bellekte olduğu için
uygulama kapanırken bekleyen parti kaybolur; alarmların kendisi veritabanında olduğu için bu kabul edildi.

## 11. Alarm kuralı entity'de

**Bağlam:** "Bu alarm bu fiyatta çalar mı?" kuralı tek bir yerde olmalı ve kolay test edilmeli.

**Seçenekler:** SQL sorgusunda karşılaştırmak; bir servis sınıfında; `PriceAlert` entity'sinde.

**Karar:** `PriceAlert.IsTriggeredBy` (Above `>=`, Below `<=`; hedefe tam ulaşmak da tetikler) ve `MarkTriggered`.
Kural veritabanı veya ağ olmadan birim testiyle sınanır. Kontrol mantığı, test edilebilmesi için arka plan
servisinden ayrı bir `AlertProcessor` sınıfına alındı.

**Takas:** Bir sembolün bütün aktif alarmları belleğe okunur. Alarm sayısı çok büyürse karşılaştırma SQL'e
taşınmalı; o zaman kural iki yerde tutulmuş olur.

## 12. Tek seferlik alarm ve en az bir kez teslim

**Bağlam:** Fiyat hedefin etrafında gidip gelirken kullanıcı her dakika mesaj almamalı; bildirim de kaybolmamalı.

**Seçenekler:** Tek seferlik alarm veya yeniden kurulan alarm; alarmı bildirimden önce veya sonra pasif yapmak.

**Karar:** Tek seferlik alarm: koşul sağlanınca bildirim gider ve alarm pasif olur. Alarm, bildirim **başarıyla
gittikten sonra** ve her biri ayrı kaydedilerek pasif yapılır. Gönderim başarısız olursa alarm aktif kalır ve
sonraki fiyat partisinde tekrar denenir. Bildirim sırasında alarm silinirse oluşan
`DbUpdateConcurrencyException` partinin geri kalanını durdurmaz.

**Takas:** Bildirim gidip kayıt başarısız olursa aynı bildirim iki kez gidebilir. Bildirimin hiç gitmemesi daha
kötü olduğu için bu kabul edildi.

## 13. Bildirim arayüzü ve Telegram

**Bağlam:** Bildirim kanalı ileride değişebilir; geliştirme sırasında Telegram kurmak zorunlu olmamalı.

**Seçenekler:** Telegram'ı doğrudan çağırmak; bir arayüz ve yapılandırmaya göre seçilen uygulamaları.

**Karar:** `IAlertNotificationSender`. `Telegram:BotToken` varsa Telegram göndericisi, yoksa yalnızca loga yazan
gönderici kullanılır. Mesaj kullanıcının kendi `TelegramChatId`'sine, yoksa varsayılan `Telegram:ChatId`'ye gider.
Bot token istek adresinin içinde olduğu için o HttpClient'ın varsayılan HTTP logları kapatıldı ve hata mesajlarına
adres yazılmaz.

**Takas:** Kullanıcının girdiği ChatId'nin ona ait olduğu doğrulanmıyor; doğrusu bota `/start <kod>` ile hesap
eşleştirmek olurdu. ChatId'si olmayan kullanıcının alarmı (bot token varken) her partide uyarı loglayarak
tekrar denenir.

## 14. API girdi doğrulaması

**Bağlam:** Hatalı girdi ya sessizce yanlış veri üretmeli ya da açık bir hata ile reddedilmeli; ikincisi seçildi.

**Karar:**
- Alarm yalnızca fiyatı toplanan sembollere kurulabilir (`PriceSymbols`: BTC, USDTRY, EURTRY); aksi halde hiç
  çalmayacak bir alarm kaydedilirdi.
- Koşul yalnızca adıyla kabul edilir (`Above`/`Below`); `Enum.TryParse` `"99"` gibi sayıları da kabul ettiği
  için kullanılmadı.
- Hedef fiyat 0'dan büyük, `decimal(20,8)` kolonuna sığacak kadar küçük ve en fazla 8 ondalıklı olmalı.
  SQL Server fazla ondalığı hata vermeden yuvarlar (`0.000000001` → `0`); bu davranış Testcontainers testiyle
  gösterildi. Yuvarlamak yerine reddediliyor ki kaydedilen değer gönderilenle aynı olsun.
- Başka kullanıcının alarmını silmek 403 değil 404 döner; o id'nin var olduğu sızdırılmaz.
- Yanıttaki tarihler UTC olarak işaretlenir (`Z`); EF Core `datetime2` okurken türü belirsiz bırakıyor.

**Takas:** Doğrulama uç noktalarda elle yazıldı; FluentValidation gibi bir kütüphane daha düzenli olurdu.
Kullanıcı başına alarm sayısı sınırı ve sayfalama yok.

## 15. Test stratejisi

**Bağlam:** "Çalışıyor" demek yetmez; davranışı her değişiklikte otomatik doğrulamak gerekir.

**Seçenekler (veritabanı için):** EF InMemory; bellekte SQLite; gerçek SQL Server (LocalDB veya container).

**Karar:** Üç test projesi:
- `UnitTests`: ağ ve veritabanı yok (`PriceAlert` kuralı, Telegram göndericisi için isteği kaydeden sahte `HttpMessageHandler`).
- `IntegrationTests`: bellekte SQLite ve `WebApplicationFactory` ile gerçek API. SQLite ilişkisel olduğu için silinmiş
  satırı güncellemek gerçekten `DbUpdateConcurrencyException` fırlatır; EF InMemory bunu gerçekçi göstermez.
- `DatabaseTests`: Testcontainers ile gerçek SQL Server 2022; gerçek migration'lar, unutulmuş migration kontrolü,
  `decimal(20,8)` yuvarlaması ve `DateTimeOffset` sıralaması.

Mocking kütüphanesi yerine elle yazılmış sahte sınıflar kullanıldı. Düzeltilen her hata için bir regresyon testi
var; bunlardan biri hatalı kod geri getirilerek gerçekten kırıldığı görüldü.

**Takas:** SQLite testleri hızlı ama SQL Server'ın bazı davranışlarını göstermez; bu açık `DatabaseTests` ile
kapatıldı, ama o testler Docker gerektirir ve daha yavaştır. Auth ve watchlist uç noktalarının testi yok.

## 16. Sürekli entegrasyon: GitHub Actions

**Bağlam:** Geliştirme makinesinde Windows Akıllı Uygulama Denetimi yeni derlenen dll'leri engelliyor; test
dll'leri neredeyse hiç çalıştırılamadı. Bu denetimi kapatmak geri alınamadığı için seçenek değildi.

**Seçenekler:** Engeli her seferinde yeniden derleyerek aşmaya çalışmak; testleri Linux'ta çalıştırmak (CI veya Docker).

**Karar:** GitHub Actions (`ubuntu-latest`): her push'ta derleme ve bütün testler. İş akışının yalnızca okuma
yetkisi var; aynı dala art arda push edilince eski çalışma iptal edilir. Aşamalar ayrı bir dalda geliştirilip CI
yeşilse `main`'e birleştirilir. Docker kurulduktan sonra testler yerelde de Linux container'ında çalıştırılabiliyor.

**Takas:** Geri bildirim yerelden yavaş (yaklaşık 1-2 dakika).

## 17. Docker ve docker compose

**Bağlam:** Projenin başka bir makinede tek komutla, Windows'a ve LocalDB'ye bağlı olmadan çalışması.

**Karar:**
- **Çok aşamalı Dockerfile:** `sdk:8.0` imajında derlenir, yalnızca çıktı `aspnet:8.0` çalışma zamanı imajına
  kopyalanır. Önce proje dosyaları kopyalanıp restore edilir; kod değişince paket katmanı önbellekten gelir.
- **Root olmadan:** Container, resmi imajdaki yetkisiz `app` kullanıcısıyla çalışır ve 8080'i dinler.
- **Compose:** `sqlserver` → `migrator` → `api` sırası; SQL Server sağlıklı olmadan migration, migration başarıyla
  bitmeden API başlamaz. Veritabanı portu dışarı açılmaz; veriler adlandırılmış bir volume'da kalır.
- **`/health`:** Veritabanına bağlanılabiliyor mu diye bakar; bağlanılamıyorsa 503 döner.

- **Yeniden başlatma:** `sqlserver` ve `api` `restart: unless-stopped`. `depends_on` yalnızca `docker compose up`
  sırasında geçerli; Docker yeniden başladığında yalnızca API geri gelseydi veritabanısız kalırdı.
- **Şifre ve bağlantı dizesi:** Şifre bağlantı dizesinde tek tırnak içinde, böylece `;` onu kesmez. `$`, `'`,
  `"`, `#` ve boşluk desteklenmiyor (compose `$`'ı değişken sanıp siler); `.env.example`'da belgeli.

**Takas:** API, SQL Server'a yönetici hesabıyla (`sa`) bağlanıyor; doğrusu yalnızca gereken yetkileri olan ayrı
bir kullanıcı olurdu. Compose yalnızca yerel geliştirme için; HTTPS, ters vekil sunucu (reverse proxy) ve
yedekleme yok.

Docker ile API'yi veritabanı olmadan çalıştırmak bir hatayı ortaya çıkardı: veritabanı kontrolü hata verince
TCMB döngüsü yarıda kesiliyor, EURTRY önbelleğe hiç girmiyordu. Önbellek artık veritabanından önce ve ayrı
dolduruluyor (bkz. 5).

## 18. Migration'ların uygulanması

**Bağlam:** Container'da veritabanı şeması API'den önce hazır olmalı.

**Seçenekler:**

| Seçenek | Artısı | Eksisi |
| --- | --- | --- |
| API açılışta `Database.Migrate()` | En basiti | Birden fazla örnek aynı anda migration'a girişebilir; API'nin şema değiştirme yetkisi olmalı |
| Ayrı migration bundle container'ı | Bir kez ve API'den önce çalışır; üretimdeki desene yakın | Daha fazla yapılandırma |
| Elle uygulamak | — | Unutulmaya açık |

**Karar:** EF Core migration bundle (`efbundle`), compose'da bir kez çalışıp çıkan `migrator` servisi. Bir
`IDesignTimeDbContextFactory` ve başlangıç projesi olarak Infrastructure sayesinde EF, `Program.cs`'i
çalıştırmadan DbContext oluşturur; bundle derlenirken ve çalışırken JWT anahtarı gibi uygulama ayarları gerekmez.

**Takas:** Tasarım zamanı fabrikasındaki varsayılan bağlantı dizesi `appsettings.json`'dakinin kopyası; iki
yerde tutuluyor.
