# Mail-Driven IT Manager Cockpit

IT müdürünün Gmail kutusuna düşen Service Desk ticket maillerini otomatik okuyup ayrıştıran,
ticket'ları bir yönetim panelinde gösteren, çalışanlara atama/hatırlatma yapılmasını sağlayan ve
hibrit çalışma takvimini tek ekranda toplayan iç kullanıma yönelik web uygulaması.

> **Tixbox'a hiçbir yazma işlemi yapılmaz.** Panel içindeki ticket durumu yalnızca yönetim
> takibi içindir; Tixbox durumunu değiştirmez.

## Dokümantasyon

| Doküman | İçerik |
|---------|--------|
| [docs/revised-scope.md](docs/revised-scope.md) | Daraltılmış kapsam — eski SRS ile çelişkide **önceliklidir** |
| [docs/email-parser-contract.md](docs/email-parser-contract.md) | `TicketMailParser` girdi/çıktı sözleşmesi, regex'ler, uyarı kodları |
| [docs/db-schema.md](docs/db-schema.md) | 19 tablo, indeksler, durum makinesi, silme politikası |
| [docs/mvp-implementation-plan.md](docs/mvp-implementation-plan.md) | Uygulama planı ve adımlar |
| [docs/open-questions.md](docs/open-questions.md) | Cevaplanması gereken sorular + uygulanan geçici varsayımlar |
| [docs/gmail-setup.md](docs/gmail-setup.md) | **Gerçek Gmail kutusuna bağlanma adımları** |
| [docs/kullanim-kilavuzu.md](docs/kullanim-kilavuzu.md) | **Son kullanıcı kılavuzu** — kurulum, Gmail bağlama, kullanıcı yönetimi, yedekleme |

Kaynak belgeler: `docs/it-panel-srs.pdf`, `docs/ornek-ticket-maili.docx`.

## Teknoloji

**Backend** — .NET 8 · ASP.NET Core Web API · EF Core 8 · SQL Server · Hangfire · Serilog ·
FluentValidation · Swagger · Google.Apis.Gmail.v1
**Frontend** — React 18 · TypeScript · Vite · Material UI · React Router · TanStack Query ·
React Hook Form · Zod · Axios

Mimari: **modular monolith** — `Api → Infrastructure → Application → Domain`.
Mikroservis veya message broker yoktur.

```
backend/
├─ src/
│  ├─ ItCockpit.Domain/          entity, enum, durum geçiş matrisi, TicketNumber, isim normalizasyonu
│  ├─ ItCockpit.Application/     parser, servisler, DTO, soyutlamalar
│  ├─ ItCockpit.Infrastructure/  EF Core, Gmail, Hangfire, mock sağlayıcılar, seed
│  └─ ItCockpit.Api/             controller, auth, DI, Swagger
└─ tests/ItCockpit.Tests/        xUnit — parser, domain ve servis testleri
frontend/                        React + Vite + MUI
```

## Gereksinimler

- **.NET 8 SDK** (8.0.4xx)
- **Node.js 20+**
- **SQL Server** (LocalDB/Express/Developer) — varsayılan `localhost`, Windows kimlik doğrulama

## Çalıştırma

### Kolay yol

Depo kökündeki **`baslat.cmd`** dosyasına çift tıklayın. API ve arayüzü ayrı pencerelerde
başlatır, hazır olunca tarayıcıyı açar. Zaten çalışıyorsa yeniden başlatmaz.

> Bu makinede .NET 8 SDK ve Node.js **kullanıcı dizinine** kuruludur (yönetici izni
> gerekmesin diye), bu yüzden düz `dotnet` / `npm` komutları PATH'te bulunmaz.
> `baslat.cmd` PATH'i kendisi ayarlar. Makine geneline kurulum yapılırsa da çalışmaya devam eder.

Kapatmak için açılan iki pencereyi kapatmanız yeterli.

### Masaüstü kullanımı (widget)

İki parça var, ikisi de aynı uygulamayı gösterir:

**1. Uygulama penceresi (PWA)** — tarayıcı arayüzü olmadan, kendi ikonuyla açılır.
Kurmak için: <http://localhost:5173> adresini Edge'de açın →
**⋯ menüsü → Uygulamalar → Bu siteyi uygulama olarak yükle**.
Görev çubuğuna sabitlenir, çift tıkla açılır.

**2. Özet kutusu** — `ozet-kutusu.cmd` dosyasına çift tıklayın.
Masaüstünde **her zaman üstte** duran küçük bir kutu açılır:

- Atanmamış / uzun süredir açık / devam eden ticket sayıları
- Bugün ofiste, evde, izinli kişi sayısı
- Plan göndermeyenler ve veri uyumsuzluğu uyarıları
- Atanmamış ticket'ların ilk dördü
- Ekipten gelen son güncelleme

60 saniyede bir kendini yeniler. Herhangi bir sayıya tıklamak ilgili ekranı tam panelde açar.
Önce `baslat.cmd` çalışıyor olmalı.

> Özet kutusu ana uygulamayla **aynı tarayıcı profilini** kullanır; bir kez giriş yapmanız yeterlidir.

### Başka bir bilgisayara vermek (taşınabilir paket)

```bash
powershell -ExecutionPolicy Bypass -File scripts\paket-olustur.ps1
```

`paket\IT-Yonetim-Paneli\` klasörünü üretir (~119 MB). Bu klasör **kendi kendine yeterlidir**:

| Normalde gereken | Pakette |
|---|---|
| .NET 8 | İçinde (self-contained) |
| Node.js | Gerekmez — arayüz API tarafından sunulur |
| SQL Server | Gerekmez — veriler klasördeki `it-cockpit.db` dosyasında (SQLite) |

Karşı taraf klasörü kopyalayıp **`Baslat.cmd`** dosyasına çift tıklar; tarayıcı açılır.
Kurulum ve yönetici izni gerekmez.

**Sınırları bilerek kabul edilmiştir:**

- Veritabanı o bilgisayara özeldir — **ekip verisi paylaşılmaz.** Ortak veri isteniyorsa
  API'nin tek bir yerde çalışıp diğerlerinin ona bağlanması gerekir.
- SQLite modunda migration kullanılmaz; şema modelden (`EnsureCreated`) oluşturulur.
  Şema değiştiğinde `it-cockpit.db` silinip yeniden oluşturulur.
- Pakette Gmail `Mock` modundadır; gerçek Gmail için `credentials.json` ve
  [gmail-setup.md](docs/gmail-setup.md) adımları gerekir.
- Giriş hâlâ `Mock` — parola yoktur, kullanıcı listeden seçilir.

### Ağ üzerinden paylaşmak

`paylas.cmd` uygulamayı yerel ağa açar (yalnızca 5173; API dışarı açılmaz).
Güvenlik duvarı kuralını **yönetici olarak** açmanız gerekir:

```bash
New-NetFirewallRule -DisplayName "IT Cockpit (5173)" -Direction Inbound -Protocol TCP -LocalPort 5173 -Action Allow -Profile Domain,Private
```

> Uygulamada parola doğrulaması yoktur; adresi bilen herkes **yönetici olarak da** girebilir.
> Kurumsal ağda paylaşmadan önce bunu dikkate alın.

### Elle

### Backend

```bash
cd backend && dotnet run --project src/ItCockpit.Api --urls http://localhost:5080
```

Açılışta migration uygulanır ve seed verisi yazılır (`Database:MigrateOnStartup`,
`Database:SeedOnStartup`). Swagger: <http://localhost:5080/swagger>

### Frontend

```bash
cd frontend && npm install && npm run dev
```

<http://localhost:5173> — Vite, `/api` isteklerini `localhost:5080` adresine proxy'ler.

### Testler

```bash
cd backend && dotnet test
```

## Geliştirme modu (varsayılan)

| Alan | Varsayılan | Not |
|------|-----------|-----|
| `Auth:Provider` | `Mock` | Giriş ekranında kullanıcı seçilir, şifre yok. Son kullanıcı paketinde `Local` (parola) kullanılır |
| `Gmail:Provider` | `Mock` | `src/ItCockpit.Api/MailFixtures/*.txt` okunur |
| `Reminders:Provider` | `Mock` | Mail gönderilmez; `bin/.../outbox/*.eml` yazılır |
| `Hangfire:EnableServer` | `false` (Development) | Zamanlanmış job kapalı; `POST /api/ingestion/run` ile elle tetiklenir |

## Üretim yapılandırması

Taşınabilir paketin ürettiği ayar (`scripts/paket-olustur.ps1`):

```jsonc
{
  "Database":  { "Provider": "Sqlite", "MigrateOnStartup": true, "SeedOnStartup": true },
  "Auth":      { "Provider": "Local" },          // e-posta + parola
  "Gmail":     { "Provider": "Google", "MailboxAddress": "", "Mailboxes": [] },
  "Reminders": { "Provider": "Mock" },
  "Hangfire":  { "EnableServer": true, "UseMemoryStorage": true }
}
```

Posta kutuları **arayüzden** eklenir (`Gmail.Mailboxes` ayarı), dosyaya yazılmaz.
`MailboxAddress` bilinçli olarak boştur: temel `appsettings.json`'daki örnek adres devralınırsa
yetkilendirilmemiş bir kutu için ilk okumada kalıcı hata kaydı oluşur.

Şirket SSO'su için `Auth:Provider = "Google"` + `GoogleClientId` + `AllowedDomains` kullanılır;
Menarini Workspace doğrulanmamış uygulamalara izin vermediği için bugün devrede değildir.

`credentials.json` (Google OAuth istemcisi) gereklidir ve **repoda tutulmaz** (`.gitignore`).
Paket betiği bu dosyayı API klasöründen kopyalar. `token-store` klasörü pakete **hiçbir zaman**
girmez — o dosyalar geliştirme makinesindeki hesapların Gmail erişim jetonlarıdır.

Gmail kapsamları ayrı kimlik bağlamlarındadır: okuma `gmail.readonly`, gönderim `gmail.send`.
Sorgu üç filtreyle daraltılır (label + gönderen + konu kalıbı); gelen kutusunun tamamı taranmaz.

## Ayarlar (çalışma zamanında değiştirilebilir)

`AppSettings` tablosundan okunur, `GET/PUT /api/settings` ile yönetilir:

| Anahtar | Varsayılan | Anlamı |
|---------|-----------|--------|
| `Aging.StaleAfterDays` | 2 | "Güncelleme bekliyor" eşiği |
| `Aging.OldAfterDays` | 5 | "Uzun süredir açık" eşiği |
| `Aging.CriticalAfterDays` | 7 | Kritik eşik |
| `Schedule.RequiredOfficeDays` | 3 | Haftalık asgari ofis günü |
| `Schedule.RequiredHomeOfficeDays` | 2 | Haftalık azami home office günü |
| `Schedule.LockDayOfWeek` / `LockTimeLocal` | Friday / 17:00 | Plan gönderim kilidi |
| `Gmail.PollIntervalMinutes` | 5 | Mail okuma aralığı (dakika) |
| `Gmail.Mailboxes` | — | Okunacak posta kutuları, satır başıyla ayrılmış. Yönetim ekranından düzenlenir |

Aging eşikleri, okuma sıklığı ve takvim kuralları `/manager/admin` → **Ayarlar** sekmesinden
değiştirilebilir; JSON düzenlemek gerekmez.

> Tixbox'tan SLA verisi gelmediği için sistem **hedef tarih veya SLA üretmez**. Arayüzde
> "SLA gecikmesi" değil, "Uzun süredir açık" / "Güncelleme bekliyor" ifadeleri kullanılır.

## Ekranlar

**Yönetici** — `/manager/dashboard` · `/manager/tickets` · `/manager/tickets/:id` ·
`/manager/team-schedule` · `/manager/reminders` · `/manager/reminder-history` · `/manager/admin`
**Çalışan** — `/employee/my-tickets` · `/employee/my-schedule`
**Ortak** — `/login` · `/parola-degistir` · `/unauthorized` · `/error`

## Notlar

- Aynı ticket birden fazla kez forward edilse bile **tek kayıt** oluşur; ek mailler
  `TicketMailSources` altında izlenir (4 aşamalı duplicate kontrolü).
- Forward edilmiş mailde açılış tarihi **en içteki** orijinal zarftan alınır.
- Hatırlatma maili müdürün **açık onayı** olmadan gönderilmez (`confirmed: true` zorunlu).
- `AuditLogs` hiçbir koşulda silinmez.
- Çalışan yalnızca kendine atanmış ticket'ları görür; kapsam daraltması sunucu tarafında
  zorlanır. Durumu güncellediğinde müdür dashboard'unda **"Ekipten gelen güncellemeler"**
  bölümüne düşer.
- Ticket listesindeki **"Okunduğu kutu"** sütunu, kaydın hangi posta kutusu okunurken
  bulunduğunu gösterir. Aynı mail birden fazla kutuya düştüyse hepsi listelenir; tam adres
  ipucunda görünür. Detayda her mail kaynağı için ayrı ayrı yazar.
- Arama kutusu ticket numarası, talep eden, uygulama, açıklama **ve posta kutusu** üzerinde
  çalışır — posta kutusu adresi yazılarak o kutudan okunanlar süzülebilir.
- Müdür **"Elle ticket ekle"** ile panele düşmemiş bir Tixbox kaydını numarasıyla girebilir.
  Bu işlem Tixbox'ta ticket açmaz; kayıt listede *"elle eklendi"* olarak işaretlenir ve aynı
  numara ikinci kez eklenemez.
