# Revised Scope — Mail-Driven IT Manager Cockpit

> Bu doküman, `docs/it-panel-srs.pdf` (30 Temmuz 2026, "Ticket & Sprint Raporu Yapay Zekâ Analiz
> Sistemi") dokümanının **daraltılmış ve yerine geçen** kapsam tanımıdır.
> Çelişki durumunda **bu doküman önceliklidir**.

Son güncelleme: 2026-08-10

---

## 1. Tek cümlelik tanım

IT müdürünün Gmail kutusuna düşen Service Desk ticket maillerini otomatik okuyup ayrıştıran,
ticket'ları bir yönetim panelinde gösteren, çalışanlara atama/hatırlatma yapılmasını sağlayan ve
hibrit çalışma takvimini tek ekranda toplayan **iç kullanıma yönelik web uygulaması**.

## 2. Kapsam içi (MVP)

| # | Yetenek | Not |
|---|---------|-----|
| 1 | Gmail'den filtreli ticket maili okuma | Label + gönderen + subject kalıbı ile daraltılmış |
| 2 | Mail ayrıştırma (deterministik parser) | `TicketMailParser`, LLM yok |
| 3 | Forward edilmiş mailde orijinal zarfı bulma | Açılış tarihi = orijinal tarih |
| 4 | Duplicate koruması | 4 aşamalı; ikinci kayıt açılmaz |
| 5 | Müdür dashboard'u | 8 metrik kartı + 6 bölüm |
| 6 | Ticket atama / yeniden atama / durum / not | Yalnızca panel içi takip durumu |
| 7 | Hatırlatma maili (preview → onay → gönderim) | Onaysız gönderim yok |
| 8 | Hibrit çalışma takvimi (employee + manager) | Haftalık plan, onay, override |
| 9 | Denetim kaydı (AuditLogs) | Kim, ne zaman, neyi değiştirdi |
| 10 | Elle ticket ekleme | Tixbox numarasıyla; Tixbox'a **yazmaz**, bkz. §11 |
| 11 | Ticket arama | Numara, talep eden, uygulama, açıklama ve okunduğu posta kutusu |
| 12 | Ticket'ın okunduğu posta kutusunu gösterme | Liste sütunu + detayda kaynak başına, bkz. §8.b |

## 3. Kapsam dışı — açıkça yapılmayacak

- **Tixbox üzerinde create / update / delete.** Sistem Tixbox'a hiçbir yazma işlemi yapmaz.
  Tixbox'a tek dokunuş, ticket mailindeki derin bağlantının (`ExternalUrl`) yeni sekmede
  açılmasıdır.
- **Tixbox API / veritabanı entegrasyonu.** Erişim yok. Tek veri kaynağı Gmail'dir.
- **LLM / yapay zekâ analizi.** Eski SRS'in merkezindeki LLM boru hattı, güven skoru, "Onay
  Bekliyor" kuyruğu, prompt injection savunması ve provider abstraction katmanı **bu sürümde
  yoktur**. Tüm çıkarım deterministik regex/şablon iledir.
- **Sprint raporu (PDF/Google Docs/Drive) alma ve analizi.** Drive API, Docs API, OCR,
  Document AI kapsam dışı.
- **Rapor tutarsızlık kontrolleri (VC01–VC08).** Eski SRS'in 8 veri kalite kontrolü kapsam dışı.
- **SLA / hedef tarih hesabı.** Tixbox'ta SLA verisi olmadığı için **sahte SLA üretilmez**
  (bkz. §5).
- ~~**Çalışan ticket ekranı.**~~ — Sonradan kapsama alındı, bkz. §7.
- **Otomatik mail gönderimi.** Her gönderim müdür onayına bağlıdır.
- **Einstein numarası.** Hiçbir örnekte görülmedi; alan modele **eklenmedi** (bkz. open-questions).

## 4. Eski SRS ile bilinçli sapmalar

| Konu | Eski SRS | Bu kapsam | Gerekçe |
|------|----------|-----------|---------|
| Mimari | Clean Architecture 4 katman + MediatR | Modular monolith, 4 proje, MediatR yok | Ekip küçük, CQRS overhead'i gereksiz |
| Ön yüz | Next.js + Tailwind + shadcn/ui | React 18 + Vite + **Material UI** | Prompt kararı |
| Veri çıkarma | Regex → LLM → güven skoru | Yalnızca regex/şablon | LLM kapsam dışı |
| Kaynaklar | Gmail + Drive + Docs | Yalnızca Gmail | Kapsam daraltıldı |
| Gecikme | SLA tablosundan hedef tarih | Configurable **aging** kuralı | SLA verisi yok |
| Tablo sayısı | 22 | 21 | Einstein/LLM tabloları çıkarıldı |
| Roller | 5 rol | 3 rol (Admin, Manager, Employee) | MVP sadeleştirmesi |
| Mail gövdesi | Saklanmaz (KVKK) | **Description saklanır**, ham gövde saklanmaz | Panelde açıklama gösterilmesi zorunlu |

## 5. "Geciken" yerine "Aging" kuralı — zorunlu tasarım kararı

Tixbox'tan SLA süresi, hedef tarih veya termin bilgisi **gelmiyor**. Ticket mailinde yalnızca
`Priority: 2` var. Bu nedenle:

- Sistem **hiçbir yerde** "SLA gecikmesi", "termin aşımı" veya "hedef tarih" ifadesi kullanmaz.
- Bunun yerine `AppSettings` üzerinden değiştirilebilen üç eşik kullanılır:

| Ayar anahtarı | Varsayılan | Arayüzdeki etiket |
|---------------|-----------|-------------------|
| `Aging.StaleAfterDays` | 2 | "Güncelleme bekliyor" |
| `Aging.OldAfterDays` | 5 | "Uzun süredir açık" |
| `Aging.CriticalAfterDays` | 7 | "Uzun süredir açık (kritik)" |

- `StaleAfterDays` → `UpdatedAtUtc` üzerinden hesaplanır.
- `OldAfterDays` / `CriticalAfterDays` → `OriginalSentAtUtc` üzerinden hesaplanır.

## 6. Durum semantiği — kullanıcıya gösterilecek uyarı

Panelde ticket durumu değiştirmek Tixbox'ı etkilemez. Ticket detay ekranında ve durum değiştirme
diyaloğunda şu metin **kalıcı olarak** gösterilir:

> Bu durum yalnızca yönetim panelindeki takip durumudur. Tixbox durumunu değiştirmez.

## 7. Roller

| Rol | Yetki |
|-----|-------|
| `ADMIN` | Tüm Manager yetkileri + AppSettings, kullanıcı/rol yönetimi, Gmail sync durumu |
| `MANAGER` | Dashboard, **tüm** ticket'lar, atama, her durum geçişi, not, hatırlatma gönderimi, takvim onayı/override |
| `EMPLOYEE` | **Kendine atanmış** ticket'lar (görme, `IN_PROGRESS`/`COMPLETED`, not) + kendi haftalık çalışma planı |

### Çalışan ticket erişimi

Başlangıçta kapsam dışıydı; sonradan eklendi.

- Çalışan yalnızca `AssigneeUserId == kendisi` olan ticket'ları görür. Kapsam daraltması
  **sunucu tarafında** zorlanır (`TicketService`); istemcinin gönderdiği filtreye güvenilmez.
  Doğrudan id ile başkasının ticket'ı istenirse **403** döner.
- Çalışan kendi ticket'ının durumunu **ileri ve geri** alabilir:

  | Geçiş | İzin |
  |-------|------|
  | `ASSIGNED → IN_PROGRESS` | ✅ işi üstlen |
  | `IN_PROGRESS → COMPLETED` | ✅ tamamla |
  | `IN_PROGRESS → ASSIGNED` | ✅ beklemeye al |
  | `COMPLETED → IN_PROGRESS` | ✅ yeniden aç |
  | `→ UNASSIGNED` | ❌ müdürde — çalışan işi sıraya geri atamaz |
  | `→ ARCHIVED` | ❌ müdürde |

  Geri alma bilinçli olarak açıktır: yanlış tıklamayı düzeltmek için müdüre başvurmak
  gerekmemelidir. Her geçiş `TicketStatusHistory`'ye yazılır ve müdür dashboard'una düşer.
- Çalışan **atama yapamaz** — kendi işini başkasına devredemez.
- Çalışanın yaptığı her durum değişikliği müdür dashboard'undaki
  **"Ekipten gelen güncellemeler"** bölümüne düşer (kim, hangi ticket, hangi geçiş, notu ne).
  Müdürün kendi işlemleri bu listeye girmez.

## 8. Kimlik doğrulama

`Auth:Provider` ayarı üç değer alır:

| Değer | Kullanım | Nasıl çalışır |
|-------|----------|----------------|
| `Local` | **Son kullanıcı kurulumlarının varsayılanı** | E-posta + parola. Hesaplar bu veritabanında. |
| `Mock` | Geliştirme ve testler | Kullanıcı listeden seçilir, parola yok. |
| `Google` | Şirket SSO'su | OIDC + `Auth:AllowedDomains` kısıtı. Menarini Workspace doğrulanmamış uygulamalara izin vermediği için bugün kullanılamıyor (bkz. open-questions). |

### Yerel parola doğrulaması (`Local`)

Uygulama şirket ağında **tek bir makinede** çalışıp ekibe tarayıcıdan açıldığı için kimlik
doğrulaması yereldir; dış bir kimlik sağlayıcısına bağlanılmaz.

- Parola **açık saklanmaz**: PBKDF2-HMAC-SHA256, 600.000 yineleme, kayıt başına rastgele tuz.
  Özet kendi parametrelerini içerir (`pbkdf2-sha256$<iter>$<salt>$<hash>`), böylece yineleme
  sayısı ileride artırılabilir ve eski parolalar doğrulanmaya devam eder.
- **İlk açılış:** sistemde hiç parola yoksa giriş yerine *ilk kurulum* ekranı çıkar; yönetici
  parolasını belirler ve hesabı `ADMIN` rolüne yükseltilir. Kurulum tamamlanınca bu uç kapanır —
  aksi halde ikinci bir kullanıcı yöneticiliği devralabilirdi.
- **Oturumlar sunucu tarafında** tutulur (`UserSessions`); token'ın kendisi değil SHA-256 özeti
  saklanır. Her istekte doğrulanır, böylece pasifleştirilen kullanıcının oturumu anında düşer.
  Ömür 14 gün.
- **Kilitlenme:** 5 hatalı denemeden sonra hesap 15 dakika kilitlenir.
- Hatalı parola, olmayan kullanıcı ve pasif kullanıcı **aynı** mesajı döndürür — hangi
  adreslerin kayıtlı olduğu sızdırılmaz.
- Yönetici kullanıcı eklediğinde bir **başlangıç parolası** verir; kullanıcı ilk girişinde
  değiştirmek zorundadır (`MustChangePassword`), böylece yönetici parolayı bilmeye devam etmez.
- Parola değiştirmek veya yöneticinin sıfırlaması **diğer tüm oturumları kapatır**.
- Son yönetici pasifleştirilemez (`LAST_ADMIN`); aksi halde uygulama yöneticisiz kalırdı.

### Roller

| Rol | Yetki |
|-----|-------|
| `EMPLOYEE` | Yalnızca kendine atanmış ticket'lar; durum ve not |
| `MANAGER` | Tüm ticket'lar, atama, hatırlatma, ekip takvimi, yönetim ekranı |
| `ADMIN` | Yöneticinin tamamı |

### Yönetim ekranı

`/manager/admin` altında üç sekme: **Kullanıcılar** (ekleme, parola sıfırlama,
aktif/pasif), **Posta kutuları** (ekleme, Google yetkilendirme, durum, elle okuma),
**Ayarlar** (aging eşikleri, okuma sıklığı, takvim kuralları). Bu işlemler daha önce yalnızca
Swagger'dan veya JSON dosyası düzenleyerek yapılabiliyordu.

## 8.b Çoklu posta kutusu ve otomatik atama

Sonradan kapsama alındı.

### Birden fazla kutu okunur

Ticket maili bir **gruba** gittiği için tek kutu okumak eksik kalıyordu: bir ticket müdürün
kutusuna hiç düşmeden bir çalışanın kutusunda olabiliyor. Listedeki her kutu sırayla okunur.

Kutu listesi `AppSettings` tablosunda (`Gmail.Mailboxes`) tutulur ve **yönetim ekranından**
düzenlenir; ayar hiç yazılmamışsa `appsettings.json`'daki listeye düşülür. Hiçbir yerde kutu
tanımlı değilse okuma yapılmaz ve **hata da üretilmez** — yeni kurulumda kullanıcı daha hiçbir
şey yapmadan hata görmemeli.

- Her kutu **ayrı ayrı** OAuth onayı gerektirir; token kutu adresine göre saklanır.
- Her kutunun kendi `GmailSyncStates` kaydı vardır.
- Bir kutunun hatası (ör. henüz yetkilendirilmemiş) **diğerlerini durdurmaz**; hata o kutunun
  kaydına yazılır ve sonuçta ayrıca raporlanır.
- Aynı ticket birden fazla kutuda bulunursa **tek kayıt** açılır (duplicate anahtarı #2);
  her kutu ayrı `TicketMailSources` satırı olarak izlenir (`SourceMailbox`).

Kutu bilgisi arayüzde de görünür — kaç kutu okunduğunda hangi kaydın nereden geldiği
karışabildiği için:

- Ticket listesinde **"Okunduğu kutu"** sütunu. Yer kazanmak için adresin `@` öncesi yazılır,
  tam adres ipucunda (tooltip) görünür. Aynı ticket iki kutuda bulunduysa **ikisi de** listelenir.
- Ticket detayında "Mail kaynakları" bölümünde her kaynak için `Okunduğu kutu: <adres>` satırı.
- Elle eklenen kayıtlarda mail kaynağı olmadığı için sütunda *"Elle eklendi"* yazar.
- Arama kutusuna adresin bir parçası yazılarak o kutudan okunanlar süzülebilir.

> **KVKK notu:** Çalışan kutularının okunması, tek yöneticinin kutusunu okumaktan farklı bir
> yetki seviyesidir. Filtre yine dar (gönderen + konu kalıbı) ve her çalışan kendi onayını
> kendisi verir, ancak bu adım hukuk/İK onayı gerektirir — bkz. open-questions F1.

### Kişiye özel ticket'lar otomatik atanır

Ticket maili tek bir kişiye gönderilmişse sorumlu zaten bellidir; müdürün atamasını beklemez.

| Orijinal `To:` | Sonuç |
|----------------|-------|
| Tek alıcı, sistemde tanımlı aktif kullanıcı | `ASSIGNED` + o kişiye atanır, `AutoAssigned = true` |
| Tek alıcı ama tanınmayan adres | `UNASSIGNED` — tahmin yürütülmez |
| Birden fazla alıcı (grup) | `UNASSIGNED` — atama müdürde |

- Atama kaydı `AssignedByUserId = null` (sistem) ve *"Kişiye özel mail — otomatik atandı"* notuyla oluşur.
- **Müdür bu ticket'ları yine görür**; listede ve detayda "otomatik atandı" ibaresi çıkar,
  böylece kendi atamadığını ayırt eder ve gerektiğinde yeniden atayabilir.
- Çalışan müdürü beklemeden işi üstlenebilir (`IN_PROGRESS`).
- `Gmail:AutoAssignDirectTickets = false` ile tamamen kapatılabilir.

## 9. Gmail erişim ilkeleri

- Scope: analiz için `gmail.readonly`, gönderim için `gmail.send` — **ayrı istemci bağlamları**.
- Sorgu daraltması (üçü birden): `label:<Auth:TicketLabel>` + `from:ticket@menarini.com` +
  `subject:"New Ticket n."`
- Tüm gelen kutusu **taranmaz**. `historyId` / `GmailSyncStates` ile artımlı okuma yapılır.
- Job aralığı: `Gmail:PollIntervalMinutes`, varsayılan **5**.

## 10. MVP kabul kriterleri (Definition of Done)

1. Mock login çalışır.
2. `docs/ornek-ticket-maili.docx` içeriği fixture olarak parse edilir.
3. `ExternalTicketNumber = I260729_000144`
4. `RequesterName = Merve Turcan`
5. `Priority = 2`
6. `OriginalSentAtUtc = 2026-07-29 11:47` (forward tarihi 30.07.2026 13:33 **kullanılmaz**)
7. Ticket `UNASSIGNED` + `AssigneeUserId = null` olarak oluşur.
8. Dashboard'da "Atanmamış ticket" bölümünde görünür.
9. Mock çalışana atanabilir → `ASSIGNED`.
10. `IN_PROGRESS` ve `COMPLETED` yapılabilir.
11. Haftalık çalışma takvimi ekranı çalışır (employee gönderim + manager matris).
12. Hatırlatma preview'ı açılır, onaylanır, `ReminderDeliveries` kaydı oluşur.

## 11. Elle ticket ekleme

Ticket maili panele düşmediğinde (kutuya hiç gelmemiş, filtreye takılmış veya telefonla
bildirilmiş) müdür kaydı elle açabilir. **Bu Tixbox'ta ticket açmaz**; yalnızca panelde takip
kaydı oluşturur ve arayüzde bu açıkça yazar.

- Numara mailden gelenle **aynı kurala** tabidir: `^[IS]\d{6}_\d{6}$`. Baş harf ticket türünü
  belirler (`I` → Incident, `S` → Talep).
- Numara daha önce kullanılmışsa `DUPLICATE_TICKET` ile reddedilir — mail sonradan gelse bile
  ikinci kayıt oluşmaz.
- Talep eden adı mailde olduğu gibi normalize edilir: `Turcan, Merve` → `Merve Turcan`.
- Açılış tarihi geleceğe verilemez (`FUTURE_DATE`); "kaç gündür açık" hesabı bu tarihe dayanır.
- Kayıt `CreatedManually = true` işaretlenir; listede *"elle eklendi"* ibaresi çıkar.
- Yalnızca müdür/yönetici ekleyebilir.
