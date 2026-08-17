# Open Questions

Kodlamayı **bloke etmeyen** ama üretime geçmeden cevaplanması gereken sorular. Her soru için
sistemde uygulanan **geçici varsayım** ve o varsayımın nerede yaşadığı belirtilmiştir.

Durum: 🔴 kritik · 🟡 önemli · ⚪ bilgi
Son güncelleme: 2026-08-04

---

## A. Ticket numarası ve mail formatı

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| A1 | Kanonik format `^[IS]\d{6}_\d{6}$` tam mı? Eski SRS `[A-Z]{1,2}` öneriyordu. `C` (Change), `P` (Problem), `R` (Request) gibi önekler var mı? | 🔴 | Yalnızca `I` ve `S` kabul edilir. Diğer önekler **reddedilir** ve loglanır. | `TicketNumber.cs` |
| A2 | `260729` gerçekten `YYMMDD` mi? Mail tarihiyle örtüşme tek örneğe dayanıyor. | 🟡 | Tarih olarak **yorumlanmıyor**; numara opak string. `OriginalSentAtUtc` maildan alınır. | — |
| A3 | `000144` gün bazlı mı yıl bazlı sayaç mı? | ⚪ | Kullanılmıyor. | — |
| A4 | Bir ticket numarası hiç değişir mi (birleştirme/bölme)? | 🟡 | Değişmez varsayıldı; duplicate anahtarı olarak kullanılıyor. Değişirse duplicate mantığı bozulur. | `TicketIngestionService.cs` |
| A5 | Subject şablonundaki 4. segment (`- N/A -`) her zaman External Reference mi? | 🟡 | Evet varsayıldı; ancak gövdedeki `External Reference:` **öncelikli**. | `TicketMailParser.cs` |
| A6 | `ApplicationName` (`ERP TR`) sabit bir listeden mi geliyor? | 🟡 | Serbest metin olarak saklanıyor, `nvarchar(200)`. Departman eşlemesi yok. | `Tickets.ApplicationName` |

## B. Mail kaynağı ve Gmail

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| B1 | Ticket mailleri için Gmail'de hazır bir label var mı? Yoksa oluşturulabilir mi? | 🔴 | `Gmail:TicketLabel` ayarı, varsayılan `Tickets`. Label yoksa job uyarı loglar ve label filtresi olmadan (yalnızca from+subject) devam eder. | `appsettings.json` |
| B2 | `ticket@menarini.com` dışında ticket maili atan gönderen var mı? | 🔴 | Tek gönderen. `Gmail:AllowedSenders` **dizi** olarak tanımlı, tek eleman içeriyor — genişletilebilir. | `appsettings.json` |
| B3 | Durum güncelleme / kapanış mailleri geliyor mu? Formatları ne? | 🔴 | **Elimizde yalnızca açılış maili örneği var.** Sistem şu an yalnızca açılış maili işler. Kapanış maili gelirse `New Ticket n.` içermediği için sessizce reddedilir. Bu, panelin Tixbox kapanışlarını görememesi demektir. | `TicketMailParser.cs` (F2) |
| B4 | Mailler hangi kutudan okunacak? Birden fazla kutu olacak mı? | 🟡 | Tek kutu: `Gmail:MailboxAddress`. Çoklu kutu desteği yok. | `appsettings.json` |
| B5 | Ekli dosya gelen ticket mailleri var mı? | ⚪ | Ekler işlenmiyor, saklanmıyor. | — |
| B6 | Gmail `historyId` tabanlı artımlı okuma için Pub/Sub push kullanılacak mı, yoksa polling yeterli mi? | 🟡 | **Polling** (5 dk, `GmailSyncStates` tablosunda `LastHistoryId`). Pub/Sub kapsam dışı. | `GmailIngestionJob.cs` |

## C. Süreç ve iş kuralları

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| C1 | Aging eşikleri (2/5/7 gün) müdürün gerçek beklentisi mi? | 🟡 | `AppSettings` tablosundan okunuyor, UI'dan değiştirilebilir. **SLA olarak adlandırılmıyor.** | `AppSettings` |
| C2 | Ticket "tamamlandı" sayılması için panel dışında bir kriter var mı? | 🟡 | Yalnızca müdürün `COMPLETED` işaretlemesi. Tixbox durumu bilinmiyor ve senkronize edilmiyor. | `TicketService.cs` |
| C3 | `ARCHIVED` durumuna geçiş kuralı ne? Otomatik mi? | 🟡 | Yalnızca manuel. Otomatik arşivleme job'ı yok. | `TicketStatus.cs` |
| C4 | `External Reference` dolu geldiğinde ne anlama geliyor? | ⚪ | Sadece saklanıyor ve gösteriliyor; iş mantığı yok. | `Tickets.ExternalReference` |
| C5 | Bir ticket birden fazla kişiye atanabilir mi? | 🟡 | **Hayır** — tek `AssigneeUserId`. Geçmiş `TicketAssignments`'ta tutulur. | `Tickets.AssigneeUserId` |
| C6 | Mail `To` listesindeki 5 kişi bir "grup" — bu grup sistemde `Teams` ile eşleşmeli mi? | 🟡 | Eşleşme **yapılmıyor**. Alıcılar `TicketMailSources.OriginalRecipients` içinde ham saklanıyor. Otomatik takım ataması yok. | `TicketMailSources` |
| C7 | Ticket kapatıldıktan sonra yeni forward maili gelirse ne olmalı? | 🟡 | Ticket'ın durumu **değişmez**; yalnızca `TicketMailSources` kaydı eklenir. | `TicketIngestionService.cs` |

## D. Hibrit çalışma takvimi

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| D1 | Plan gönderim son tarihi (kilit zamanı) ne? | 🟡 | `Schedule:LockDayOfWeek` = `Friday`, `Schedule:LockTimeLocal` = `17:00`. Kilit sonrası çalışan düzenleyemez, müdür override edebilir. | `appsettings.json` |
| D2 | 3 gün ofis / 2 gün home office kuralı ihlal edilirse plan **reddedilmeli** mi, uyarı mı? | 🟡 | **Uyarı** (`ScheduleRuleViolation`), gönderim engellenmez. Müdür onay ekranında ihlali görür. | `WorkScheduleService.cs` |
| D3 | Resmî tatiller nereden gelecek? | 🟡 | `WorkCalendar` tablosuna manuel girilir. 2026 Türkiye resmî tatilleri seed edilmiştir. | `WorkCalendar` seed |
| D4 | Yıllık izin (LEAVE) İK sisteminden mi gelmeli? | 🔴 | Çalışan kendi işaretliyor; İK entegrasyonu yok. İzin doğruluğu garanti edilemez. | `WorkScheduleDays.Mode` |
| D5 | Yarım gün / saatlik izin gerekiyor mu? | ⚪ | Gün bazlı, tek mod. | `WorkMode` enum |

## E. Hatırlatma maili

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| E1 | Hatırlatma maili müdürün kendi adresinden mi, servis hesabından mı gitmeli? | 🔴 | Müdürün kendi hesabından (`gmail.send`, OAuth kullanıcı bağlamı). | `GmailReminderMailSender.cs` |
| E2 | CC/BCC gerekiyor mu? | 🟡 | `Cc` desteklenir, `Bcc` yok. | `ReminderRequest` |
| E3 | Şablon dili Türkçe mi, İngilizce mi? | ⚪ | Türkçe. `ReminderTemplates` tablosu çok şablon destekler. | `ReminderTemplates` seed |
| E4 | Aynı ticket için tekrar hatırlatma gönderiminde bekleme süresi olmalı mı? | 🟡 | Kısıt yok; ancak son gönderim tarihi preview'da gösterilir. | `ReminderDeliveries` |

## F. Güvenlik, uyum, altyapı

| # | Soru | Durum | Uygulanan varsayım | Nerede |
|---|------|-------|--------------------|--------|
| F1 | KVKK: mail gövdesinin `Description` alanı kalıcı saklanabilir mi? Eski SRS §1.8 "tam gövde saklanmamalı" diyordu. | 🔴 | **Description saklanıyor** (panelde gösterilmesi zorunlu), ham `Body` saklanmıyor. Bu, eski SRS'ten bilinçli sapmadır ve hukuk onayı gerektirir. | `Tickets.Description` |
| F1b | **Çalışanların posta kutularının okunması** hukuk/İK tarafından onaylandı mı? Tek yöneticinin kutusundan farklı bir yetki seviyesidir. | 🔴 | Uygulandı: `Gmail:Mailboxes` listesindeki her kutu okunuyor. Filtre dar (gönderen + konu kalıbı) ve **her çalışan kendi OAuth onayını kendisi veriyor** — kimse adına zorla erişilemiyor. Yine de aydınlatma metni ve onay süreci gerekir. | `GmailIngestionOptions.Mailboxes` |
| F1c | Kişiye özel gelen ticket'ın otomatik atanması kabul edilebilir mi, yoksa her atama müdürden mi geçmeli? | 🟡 | Otomatik atama **açık** (`AutoAssignDirectTickets = true`). Müdür ticket'ı görüyor, "otomatik atandı" ibaresiyle ayırt ediyor ve yeniden atayabiliyor. Tek satırla kapatılabilir. | `Tickets.AutoAssigned` |
| F2 | Google Workspace admin ekibi `gmail.readonly` + `gmail.send` scope'larını onaylar mı? | 🔴 | Onaylanacağı varsayıldı. Onaylanmazsa sistem yalnızca `MockGmailTicketSource` ile çalışır. | `GmailOptions` |
| F3 | Production domain kısıtı hangi domain(ler)? | 🟡 | `menarini.com.tr`. `Auth:AllowedDomains` dizisi. | `appsettings.json` |
| F4 | Şirket içi mi bulutta mı barındırılacak? | 🟡 | Yerel SQL Server + IIS/Kestrel varsayıldı. Docker dosyası eklenmedi. | — |
| F5 | Secret'lar nerede tutulacak? | 🔴 | Development'ta **User Secrets**, production'da environment variable. Repoda sır yok. | `README.md` |

## G. Bu oturuma özgü ortam / erişim engelleri

| # | Konu | Durum | Not |
|---|------|-------|-----|
| G1 | **Figma tasarımı** — verilen link (`figma.com/make/hqohr2htQnDuQfF15faYPH`) **parola korumalı** ("You need a password to access the Make file"). Erişilemedi. | 🔴 | Arayüz, promptta listelenen 14 component üzerinden **kendi MUI tasarımımızla** üretildi. Tasarım paylaşıldığında `theme.ts` + component katmanı üzerinden refactor edilebilecek şekilde ayrıştırıldı. Ayrıca dosya adı "LDAP Administration Panel UI" — bu uygulamanın değil, genel bir admin panel şablonunun tasarımı olabilir; doğrulanmalı. |
| G2 | Gerçek Gmail kimlik bilgisi / OAuth client yok. | 🔴 | `MockGmailTicketSource` varsayılan. `Gmail:Provider = Google` yapıldığında gerçek istemci devreye girer ama `credentials.json` gerekir. |
| G3 | Tixbox erişimi yok (teyit edildi). | ⚪ | Tasarım gereği. `ExternalUrl` yalnızca link olarak açılır. |
| G4 | `docs/Temmuz2026_Ticket_ERP.xlsx` dosyası masaüstünde bulundu ve `docs/` altına kopyalandı — promptta bahsedilmiyordu. | 🟡 | **Kullanılmadı.** İçeriğinin seed/doğrulama verisi olarak kullanılıp kullanılmayacağı belirsiz. |
| G5 | Eski SRS'in dayandığı "Temmuz 2026 ERP Sprint Raporu" PDF'i hâlâ yok. | ⚪ | Yeni kapsamda sprint raporu **kapsam dışı** olduğu için artık blokaj değil. |
