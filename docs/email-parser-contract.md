# Email Parser Contract — `TicketMailParser`

Bu doküman `TicketMailParser` sınıfının **girdi/çıktı sözleşmesidir**. Parser bağımsızdır:
veritabanı, Gmail API veya HTTP bağımlılığı yoktur — saf `RawTicketMail` → `TicketParseResult`
dönüşümü yapar ve bu sayede tek başına test edilebilir.

Referans veri: `docs/ornek-ticket-maili.docx` (gerçek, 29 Temmuz 2026 tarihli Menarini Service
Desk maili; 30 Temmuz 2026'da forward edilmiş).

---

## 1. Girdi

```csharp
public sealed record RawTicketMail(
    string  GmailMessageId,
    string  GmailThreadId,
    string  Subject,          // Gmail "Subject" header (forward'da "Fwd: ..." olabilir)
    string  From,             // dış zarf gönderen
    string  To,               // dış zarf alıcılar
    DateTime ReceivedAtUtc,   // Gmail internalDate
    string  Body,             // text/plain gövde (yoksa HTML'den düzleştirilmiş)
    IReadOnlyList<string> Links); // gövdedeki href'ler (sırayla)
```

## 2. Çıktı

```csharp
public sealed record TicketParseResult(
    bool                          IsTicketMail,
    ParsedTicket?                 Ticket,
    IReadOnlyList<ParseWarning>   Warnings,
    string?                       RejectReason);
```

`ParsedTicket` alanları:

| Alan | Tip | Kaynak | Zorunlu |
|------|-----|--------|---------|
| `ExternalTicketNumber` | string | Subject grubu `ticket` | ✅ |
| `TicketType` | enum | Numaranın ilk harfi | ✅ |
| `RequesterName` | string | Subject grubu `requester`, normalize | ✅ |
| `ApplicationName` | string | Subject grubu `app` | ✅ |
| `SubjectPriority` | int? | Subject grubu `priority` | ⚠ |
| `Priority` | int | Body `Ticket priority:` → yoksa `SubjectPriority` | ✅ |
| `CategoryPath` | string? | Body `Ticket category:` | ⚠ |
| `ExternalReference` | string? | Body `External Reference:` (`N/A` → `null`) | ⚠ |
| `SourceRequestId` | string? | Body `@REQUEST_ID@='...'` | ⚠ |
| `BodyTicketNumber` | string? | Body `ticket number is <no>` | ⚠ |
| `OriginalSentAtUtc` | DateTime | Forward `Date:` → yoksa `ReceivedAtUtc` | ✅ |
| `OriginalSender` | string | Forward `From:` → yoksa `From` | ✅ |
| `OriginalRecipients` | string[] | Forward `To:` → yoksa `To` | ✅ |
| `OriginalSubject` | string | Forward `Subject:` → yoksa `Subject` (`Fwd:` soyulmuş) | ✅ |
| `Description` | string | Gövdenin serbest metin bloğu | ✅ |
| `ExternalUrl` | string? | "Click here to see the request" href'i | ⚠ |
| `IsForwarded` | bool | Forward zarfı bulundu mu | ✅ |

---

## 3. Kabul filtresi (bu 3 koşul birlikte sağlanmalı)

Koşullardan biri sağlanmazsa `IsTicketMail = false`, `RejectReason` doldurulur, ticket **oluşturulmaz**.

| # | Kural | Uygulama |
|---|-------|----------|
| F1 | Orijinal gönderen `ticket@menarini.com` | Forward varsa **iç** `From:`, yoksa dış `From`. Karşılaştırma yalnızca `<...>` içindeki adres üzerinden, case-insensitive. |
| F2 | Subject `New Ticket n.` kalıbını içerir | `Fwd:` / `Fw:` / `RE:` önekleri soyulduktan sonra §4 regex'i eşleşmeli |
| F3 | Gövdede `Service Desk Menarini` imzası var | Case-insensitive, whitespace toleranslı |

Ek olarak F4 — ticket numarası kanonik formata uymalı: `^[IS]\d{6}_\d{6}$`.
Uymazsa mail reddedilir (`RejectReason = "TICKET_NUMBER_FORMAT"`).

> **Not (SRS §1.3 ile fark):** Eski SRS `\b[A-Z]{1,2}\d{6}_\d{6}\b` gibi esnek bir desen
> öneriyordu. Bu prompt kanonik formatı `[IS]` ile sınırlandırır — dar desen uygulanır,
> diğer önekler `open-questions.md` A1'de soru olarak açık bırakılmıştır.

---

## 4. Subject ayrıştırma

Gerçek örnek:

```
New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A - Priority: 2
```

Şablon:

```
New Ticket n. {TICKET_NO} for {REQUESTER} about {APP} - {EXTERNAL_REF} - Priority: {PRIORITY}
```

Regex (`RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`):

```regex
New\s+Ticket\s+n\.\s*(?<ticket>[A-Za-z]\d{6}_\d{6})\s+for\s+(?<requester>.+?)\s+about\s+(?<app>.+?)\s+-\s+(?<extref>.*?)\s*-\s*Priority\s*:\s*(?<priority>\d+)\s*$
```

Notlar:

- `requester` ve `app` **lazy** (`.+?`) — `about` ve son iki `-` ayırıcı çapa görevi görür.
- **Uygulama adı ile dış referans arasındaki ayırıcı boşluklu tire (`" - "`) olmak zorundadır.**
  Dış referans tire içerebildiği için (`REQ-8842`, `EU-2026-01-15`) ayırıcıyı `\s*-\s*` bırakmak
  veya dış referansı `[^-]*` ile sınırlamak, tirenin uygulama adına kaymasına yol açar
  (`SAP BW - REQ-8842 - Priority: 3` → hatalı `ApplicationName = "SAP BW - REQ"`).
  Bu senaryo `Subject_application_segment_is_isolated_from_external_reference` testiyle korunur.
- `Fwd:`/`Fw:`/`RE:`/`İLT:` önekleri regex'ten **önce** soyulur (tekrarlı önekler dahil).
- Subject sonunda ek metin varsa (`$` çapası nedeniyle) eşleşme başarısız olur; bu durumda
  `$` çapasız ikinci bir deneme yapılır ve `ParseWarning: SUBJECT_TRAILING_CONTENT` üretilir.

### `TicketType` eşlemesi

| Önek | `TicketType` |
|------|--------------|
| `I` | `INCIDENT` |
| `S` | `SERVICE_REQUEST` |

Başka bir harf → F4 reddi.

### İsim normalizasyonu — `"Turcan, Merve"` → `"Merve Turcan"`

```
1. Trim + iç boşlukları tek boşluğa indir.
2. Tam olarak bir virgül varsa:  "Soyad, Ad İkinciAd"  →  "Ad İkinciAd Soyad"
3. Virgül yoksa veya >1 virgül varsa: değer olduğu gibi bırakılır.
4. Sonuç Türkçe kültürüyle (tr-TR) title-case'e çevrilir; tamamı büyük harf olan
   girdiler ("TURCAN, MERVE") de doğru normalize olur.
```

Türkçe karakter uyarısı: `ToUpper`/`ToLower` **daima** `CultureInfo("tr-TR")` ile çağrılır
(`i` → `İ`, `I` → `ı` sorunu). Karşılaştırmalar ise `OrdinalIgnoreCase`'dir.

---

## 5. Forward zarfı ayrıştırma

Gerçek örnekte gövde şu bloğu içerir:

```
---------- Forwarded message ---------
From: ticket@menarini.com <ticket@menarini.com>
Date: Wed, 29 Jul 2026 at 11:47
Subject: New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A - Priority: 2
To: Yilmaz, Ahmet <ayilmaz@menarini.com.tr>, Oz, Dilara <doz@menarini.com.tr>, ...
```

Kurallar:

1. Forward ayırıcısı aranır (ilk eşleşen değil — **en sonuncusu**, çünkü zincirli forward'da en
   içteki orijinal maildir):
   - `---------- Forwarded message ---------` (Gmail, tire sayısı değişken)
   - `-----Original Message-----` (Outlook)
   - `Begin forwarded message:` (Apple Mail)
   - `________________________________` ardından `From:` (Outlook Web)
2. Ayırıcıdan sonraki blokta `From:` / `Date:` / `Subject:` / `To:` / `Cc:` başlıkları okunur.

   **Katlanmış (wrap edilmiş) satırlar birleştirilir.** Gmail'in `text/plain` sürümü uzun
   başlıkları ~78 karakterde böler; gerçek kutuda konu satırı şöyle ikiye ayrılır:

   ```
   Subject: New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A -
   Priority: 2
   ```

   Başlık kalıbına uymayan ve **hemen** bir başlık satırını izleyen satırlar, önceki başlığın
   devamı sayılır. Aynısı uzun `To:` listeleri için de geçerlidir.

   **Boş satırlar bloğu tek başına bitirmez.** HTML'den düzleştirilen gövdelerde başlıklar
   arasına boş satır girebilir; blok ancak sonraki dolu satır da başlık değilse biter.
   Boş satır katlama zincirini keser — böylece gövde metni başlığa yapışmaz.

   > Bu iki kural gerçek bir Gmail kutusuna bağlanınca ortaya çıktı: katlama işlenmediğinde
   > konu yarım okunuyor ve mail `SUBJECT_PATTERN_MISMATCH` ile **reddediliyordu**.
   > Regresyon koruması: `gmail-html-flattened-mail.txt` fixture'ı ve
   > `Wrapped_subject_header_in_forward_block_is_unfolded` testi.
3. `Date:` şu formatlarla denenir (hepsi `CultureInfo.InvariantCulture`):
   - `ddd, d MMM yyyy 'at' HH:mm` → `Wed, 29 Jul 2026 at 11:47`
   - `ddd, d MMM yyyy HH:mm:ss zzz` → RFC 2822
   - `ddd, d MMM yyyy HH:mm`
   - `d MMM yyyy 'at' HH:mm`
   - Son çare: `DateTime.TryParse` (invariant)
4. **Timezone:** Forward `Date:` satırında offset yoksa değer `Gmail:DefaultTimeZone`
   (varsayılan `Europe/Istanbul`, UTC+03) yerel saati kabul edilir ve UTC'ye çevrilir.
   → `29 Jul 2026 11:47 +03:00` = **`2026-07-29T08:47:00Z`**
   Panelde ve testlerde gösterilen `29.07.2026 11:47` bu değerin İstanbul saatidir.
5. Forward bulunamazsa mail doğrudan gelmiş kabul edilir: `IsForwarded = false`,
   `OriginalSentAtUtc = ReceivedAtUtc`, `OriginalSender = From`.

> **Dış zarf tarihi hiçbir koşulda `OriginalSentAtUtc` olarak kullanılmaz** (forward varsa).
> Örnekte 30 Temmuz 2026 13:33 → **kullanılmaz**; 29 Temmuz 2026 11:47 kullanılır.

---

## 6. Gövde alanları

Ayırıcı bloğun **altındaki** metin üzerinde çalışılır (forward yoksa tüm gövde).

| Alan | Desen | Örnek değer |
|------|-------|-------------|
| Priority | `^\s*Ticket priority\s*:\s*(?<v>\d+)` | `2` |
| Category | `^\s*Ticket category\s*:\s*(?<v>.+)$` | `Incidents/TixHub Categories/Applications & Services - ERP TR` |
| External Reference | `^\s*External Reference\s*:\s*(?<v>.+)$` | `N/A` → `null` |
| Body ticket no | `ticket\s+number\s+is\s+(?<v>[A-Za-z]\d{6}_\d{6})` | `I260729_000144` |
| Source request id | `@REQUEST_ID@\s*=\s*'?(?<v>[^'\s@]+)'?` | `784090` |

Tümü `IgnoreCase | Multiline`.

`External Reference` değeri `N/A`, `NA`, `-`, boş → `null` olarak normalize edilir.

### `Description` çıkarımı

```
başlangıç: "with the following description:" satırından sonraki satır
           (bulunamazsa: forward başlık bloğundan sonraki ilk boş satır)
bitiş    : ilk karşılaşılan durdurucu satır:
             - "Ticket priority:"
             - "Ticket category:"
             - "Please take it in charge"
             - "Kindest Regards"
             - "Service Desk Menarini"
             - "This is an automatic email"
```

Ardından: baştaki/sondaki boş satırlar atılır, 3+ ardışık boş satır 1'e indirilir.

Örnekten beklenen `Description`:

```
Merhaba,
Aksel Ecza Deposu'na ait cari hesapta (1001299 no.lu cari) F1 belge türü ile gelen kayıtlar negatiftir.
F1 belge türü kaydı negatif olamaz. (Fatura Kaydı)
Kontrollerinizi rica ederim.

Teşekkürler.
```

### `ExternalUrl`

Gövdedeki `Click here to see the request` metninin bağlantısı. HTML gövdede anchor'dan,
plain-text gövdede `Links` listesinden `tixcore.menarini.com` içeren ilk href seçilir.

Örnekten beklenen değer:

```
https://tixcore.menarini.com/autoconnect_mail.php?field1=5C0F051E590F056F1D&field2=&field4={07ED9C68-6172-48EA-8A58-90912B0A283E}&field5=ViewDialog&field6=I260729_000144&field7=RFC_NUMBER
```

Bulunamazsa `null` + `ParseWarning: EXTERNAL_URL_MISSING`.

---

## 7. ParseWarning kodları

| Kod | Severity | Anlamı | Ticket oluşur mu? |
|-----|----------|--------|-------------------|
| `TICKET_NUMBER_MISMATCH` | `Error` | Subject ve gövdedeki ticket numarası farklı | ✅ (subject'teki kullanılır) |
| `PRIORITY_MISMATCH` | `Warning` | Subject ve gövdedeki priority farklı | ✅ (gövdedeki kullanılır) |
| `ORIGINAL_DATE_UNPARSED` | `Warning` | Forward `Date:` çözülemedi | ✅ (`ReceivedAtUtc` kullanılır) |
| `SUBJECT_TRAILING_CONTENT` | `Info` | Subject sonunda beklenmeyen metin | ✅ |
| `EXTERNAL_URL_MISSING` | `Info` | Tixbox derin bağlantısı yok | ✅ |
| `CATEGORY_MISSING` | `Info` | `Ticket category:` yok | ✅ |
| `DESCRIPTION_EMPTY` | `Warning` | Açıklama bloğu boş | ✅ |
| `REQUEST_ID_MISSING` | `Info` | `@REQUEST_ID@` yok | ✅ |

**Kural:** Uyarı hiçbir zaman ticket oluşumunu engellemez. Uyarılar `TicketParseWarnings`
tablosuna yazılır ve müdür ekranında "Veri uyumsuzluğu" rozetiyle gösterilir.
`Error` seviyesindeki uyarılar dashboard'da ayrıca listelenir.

---

## 8. Duplicate kararı (parser değil, ingestion sorumluluğu)

Parser her zaman ayrıştırır; kaydetme kararını `TicketIngestionService` verir. Sıra:

| # | Anahtar | Eşleşirse davranış |
|---|---------|--------------------|
| 1 | `GmailMessageId` | Tamamen atla (aynı mail yeniden okundu) |
| 2 | `ExternalTicketNumber` | Yeni ticket **açma**; `TicketMailSources` kaydı ekle |
| 3 | `SourceRequestId` | Yeni ticket **açma**; `TicketMailSources` kaydı ekle |
| 4 | `Subject` + `OriginalSentAtUtc` (dakika hassasiyeti) | Yeni ticket **açma**; `TicketMailSources` kaydı ekle |

Hiçbiri eşleşmezse yeni ticket: `Status = UNASSIGNED`, `AssigneeUserId = null`.

---

## 9. Referans test vektörü (MVP kabul kriteri)

Girdi: `tests/.../Fixtures/forwarded-ticket-mail.txt` (gerçek maildan birebir üretilmiştir)

| Alan | Beklenen değer |
|------|----------------|
| `IsTicketMail` | `true` |
| `IsForwarded` | `true` |
| `ExternalTicketNumber` | `I260729_000144` |
| `TicketType` | `INCIDENT` |
| `RequesterName` | `Merve Turcan` |
| `ApplicationName` | `ERP TR` |
| `SubjectPriority` | `2` |
| `Priority` | `2` |
| `CategoryPath` | `Incidents/TixHub Categories/Applications & Services - ERP TR` |
| `ExternalReference` | `null` (`N/A`) |
| `SourceRequestId` | `784090` |
| `BodyTicketNumber` | `I260729_000144` |
| `OriginalSentAtUtc` | `2026-07-29T08:47:00Z` (= 29.07.2026 11:47 İstanbul) |
| `OriginalSender` | `ticket@menarini.com` |
| `OriginalRecipients.Length` | `5` |
| `ExternalUrl` | `https://tixcore.menarini.com/autoconnect_mail.php?...field6=I260729_000144...` |
| `Warnings` | boş |
| Oluşan ticket `Status` | `UNASSIGNED` |
| Oluşan ticket `AssigneeUserId` | `null` |
