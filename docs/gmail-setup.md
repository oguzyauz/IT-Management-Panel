# Gerçek Gmail'e Bağlanma

Bu rehber, uygulamayı `Mock` modundan çıkarıp **gerçek Gmail kutusunu okuyacak** hâle getirir.

> **Güvenlik notu:** `credentials.json` ve `token-store/` dosyaları hesap erişim bilgisi içerir.
> Bunlar `.gitignore` içindedir ve **hiçbir koşulda commit edilmemelidir.**

---

## 1. Google Cloud tarafı (yalnızca siz yapabilirsiniz)

<https://console.cloud.google.com> adresinde:

1. **Proje oluşturun** (veya var olanı seçin) — ör. `it-manager-cockpit`.
2. **APIs & Services → Library → Gmail API → Enable.**
3. **APIs & Services → OAuth consent screen**
   - Menarini Workspace hesabıyla giriş yaptıysanız **Internal** seçin (test kullanıcısı eklemeye
     gerek kalmaz). Seçenek gri ise **External** + kendinizi *Test users* listesine ekleyin.
   - Scope eklemeniz gerekmez; uygulama isteği çalışma anında yapar.
4. **APIs & Services → Credentials → Create credentials → OAuth client ID**
   - Application type: **Desktop app**
   - Oluşan istemciyi **JSON olarak indirin.**
5. İndirdiğiniz dosyayı **`credentials.json`** olarak yeniden adlandırıp şuraya koyun:

```
backend/src/ItCockpit.Api/credentials.json
```

> İndirilen dosyanın adı genelde `client_secret_1234-abcd.apps.googleusercontent.com.json`
> şeklindedir; **adını `credentials.json` yapmanız gerekir.**

### Hazır olup olmadığını nasıl anlarsınız

Tahmin etmeyin — sorun:

```bash
curl http://localhost:5080/api/ingestion/gmail-status -H "Authorization: Bearer mock:11111111-1111-1111-1111-111111111111"
```

Dosya henüz yokken:

```json
{
  "credentialsFound": false,
  "problem": "Dosya bulunamadı. ...",
  "nextStep": "credentials.json dosyasını yerleştirin."
}
```

Doğru dosya yerleştirildiğinde:

```json
{
  "credentialsFound": true,
  "credentialsValid": true,
  "clientType": "installed",
  "clientIdMasked": "***googleusercontent.com",
  "nextStep": "Gmail:Provider ayarını 'Google' yapıp API'yi yeniden başlatın."
}
```

`nextStep` alanı her zaman sıradaki tek adımı söyler. Yanlış dosya indirdiyseniz
(`Web application` veya servis hesabı anahtarı) `problem` alanı bunu açıkça yazar.

> Bu uç nokta `client_secret` değerini **okumaz ve döndürmez**; `client_id` yalnızca
> maskelenmiş hâlde görünür.

> Workspace yöneticiniz üçüncü taraf uygulamaları kısıtlıyorsa 3. adımda onay gerekebilir.
> İstenen yetkiler: `gmail.readonly` (okuma) ve `gmail.send` (hatırlatma gönderimi).

---

## 2. Uygulama ayarları

`backend/src/ItCockpit.Api/appsettings.Development.json` dosyasına ekleyin:

```json
{
  "Gmail": {
    "Provider": "Google",
    "Mailboxes": [
      "ayilmaz@menarini.com.tr",
      "doz@menarini.com.tr",
      "btufan@menarini.com.tr"
    ],
    "TicketLabel": null,
    "InitialLookbackDays": 30,
    "AutoAssignDirectTickets": true
  }
}
```

| Ayar | Açıklama |
|------|----------|
| `Provider` | `Google` gerçek Gmail, `Mock` fixture dosyaları |
| `Mailboxes` | Okunacak kutular. **Her biri ayrı ayrı yetkilendirilmelidir.** Tek kutu için `MailboxAddress` de kullanılabilir |
| `TicketLabel` | `null` bırakın. Bir Gmail etiketiyle daha da daraltmak isterseniz etiket adını yazın; etiket bulunamazsa uyarı loglanır ve yok sayılır |
| `InitialLookbackDays` | İlk çalıştırmada kaç gün geriye bakılacağı. Sonraki çalıştırmalar son senkron tarihinden devam eder |
| `AutoAssignDirectTickets` | Ticket maili tek kişiye gelmişse o kişiye otomatik atansın mı (varsayılan `true`) |

### Neden birden fazla kutu?

Ticket maili bir gruba gidiyor. Yalnızca müdürün kutusunu okursanız, ona düşmeyen ama bir
çalışanın kutusunda olan ticket'ları kaçırırsınız. Aynı ticket birden fazla kutuda bulunursa
**tek kayıt** açılır; her kutu ayrı kaynak olarak izlenir.

Bir kutu yetkilendirilmemişse veya erişilemiyorsa **diğerleri okunmaya devam eder**; hata o
kutunun durumuna yazılır.

**Hatırlatma maillerini de gerçekten göndermek isterseniz** (ilk denemede önermem):

```json
{ "Reminders": { "Provider": "Google", "FromAddress": "SIZIN_ADRESINIZ@menarini.com.tr" } }
```

---

## 3. Yetkilendirme (bir kez)

API'yi başlatın:

```bash
cd backend && dotnet run --project src/ItCockpit.Api --urls http://localhost:5080
```

Sonra yetkilendirmeyi tetikleyin:

**Her kutu için ayrı ayrı** çağırın:

```bash
curl -X POST "http://localhost:5080/api/ingestion/authorize?mailbox=ayilmaz@menarini.com.tr" -H "Authorization: Bearer mock:11111111-1111-1111-1111-111111111111"
```

Sunucunun çalıştığı makinede **tarayıcı açılır**. O kutunun sahibi kendi hesabıyla giriş yapıp
izin verir. İşlem tamamlanınca endpoint yetki verilen adresi döner:

```json
{
  "authorizedEmail": "ayilmaz@menarini.com.tr",
  "requestedMailbox": "ayilmaz@menarini.com.tr",
  "matchesRequestedMailbox": true,
  "provider": "Google"
}
```

> **`matchesRequestedMailbox: false` görürseniz dikkat:** tarayıcıda başka bir hesapla giriş
> yapılmış demektir. Bu sessizce yanlış kutunun okunmasına yol açar. Token'ı silip
> (`token-store/` altındaki ilgili dosya) doğru hesapla tekrar deneyin.

`mailbox` parametresini vermezseniz listedeki ilk kutu kullanılır.
Hangi kutuların yetkilendirildiğini `gmail-status` çıktısındaki `mailboxes` alanında görürsünüz.

Token `backend/src/ItCockpit.Api/token-store/` altına yazılır; tekrar giriş gerekmez.

> Şifrenizi uygulamaya **girmezsiniz** — giriş tamamen Google'ın kendi ekranında olur.

---

## 4. Önce kuru çalıştırma (önerilir)

Veritabanına **hiçbir şey yazmadan** parser'ın ne gördüğünü raporlar:

```bash
curl -X POST http://localhost:5080/api/ingestion/preview -H "Authorization: Bearer mock:11111111-1111-1111-1111-111111111111"
```

Her mail için dış konu, forward zarfının bulunup bulunmadığı, iç `From`/`Date`/`Subject`
satırları, kabul/red durumu ve ayrıştırılan alanlar döner. Mail gövdesi döndürülmez.

Bir mail reddediliyorsa sebebini burada görürsünüz — `rejectReason` alanı hangi filtreye
takıldığını söyler.

## 5. Mailleri okuma

Panelde **Dashboard → "Mailleri şimdi oku"** düğmesine basın, ya da:

```bash
curl -X POST http://localhost:5080/api/ingestion/run -H "Authorization: Bearer mock:11111111-1111-1111-1111-111111111111"
```

Dönen özet: kaç mail okundu, kaç ticket oluştu, kaç duplicate atlandı, kaç mail reddedildi.

---

## Hangi mailler okunur?

Gmail'e gönderilen sorgu:

```
(from:ticket@menarini.com OR "ticket@menarini.com") subject:"New Ticket n." after:YYYY/MM/DD
```

Kutunun tamamı **taranmaz**. Adres hem gönderen hem serbest metin olarak aranır — çünkü
**forward edilmiş mailde dış gönderen ileten kişidir**, orijinal gönderen yalnızca gövdedeki
zarf başlığında görünür.

Okunan her mail ayrıca parser'ın kesin filtresinden geçer (bkz.
[email-parser-contract.md](email-parser-contract.md) §3):

1. Orijinal gönderen `ticket@menarini.com` olmalı (forward varsa **iç** zarftan)
2. Konu `New Ticket n. ...` kalıbına uymalı
3. Gövdede `Service Desk Menarini` imzası olmalı
4. Ticket numarası `^[IS]\d{6}_\d{6}$` formatında olmalı

Bu koşullardan biri sağlanmazsa mail **reddedilir**, ticket oluşmaz ve sebebi
`TicketParseWarnings` tablosuna yazılır.

---

## Sorun giderme

**Her şeyden önce:** `GET /api/ingestion/gmail-status` çağırın — hangi aşamada olduğunuzu ve
sıradaki adımı doğrudan söyler.

| Belirti | Sebep / çözüm |
|---------|---------------|
| `Gmail credentials dosyası bulunamadı` | Dosya yanlış konumda veya adı `credentials.json` değil. `gmail-status` çıktısındaki `credentialsPath` tam olarak nereye bakıldığını gösterir |
| `clientType: "web"` | Yanlış istemci tipi indirilmiş. Google Cloud'da **Desktop app** tipinde yeni OAuth client ID oluşturun |
| `clientType: "service_account"` | Servis hesabı anahtarı indirilmiş. Bu uygulama kullanıcı onaylı OAuth kullanır |
| `authorize` çağrısı "yetkilendirme gerektirmiyor" diyor | `Gmail:Provider` hâlâ `Mock`. Ayarı `Google` yapıp API'yi yeniden başlatın |
| Tarayıcı açılmıyor | API'nin masaüstü oturumunda çalıştığından emin olun (servis/konteyner içinde OAuth ekranı açılamaz) |
| `access_denied` | OAuth consent screen'de hesabınız *Test users* listesinde değil, ya da Workspace yöneticisi engelliyor |
| 0 mail okundu | `InitialLookbackDays` çok kısa olabilir; ya da konu kalıbı farklı. Log'daki `Gmail sorgusu:` satırına bakın ve aynı sorguyu Gmail arama kutusunda deneyin |
| Mailler okunuyor ama ticket oluşmuyor | Parser reddediyor. `GET /api/tickets/warnings` ile red sebeplerini görün |
| Mock'a geri dönmek | `Gmail:Provider` → `Mock`. Token silmek için `token-store/` klasörünü kaldırın |

---

## Geri alma

Gerçek maillerden oluşan ticket'ları temizlemek için:

```bash
cd backend
dotnet tool run dotnet-ef database drop --force --project src/ItCockpit.Infrastructure --startup-project src/ItCockpit.Api
```

API'yi tekrar başlattığınızda şema ve seed verisi sıfırdan oluşur.
