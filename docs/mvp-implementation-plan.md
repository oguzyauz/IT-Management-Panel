# MVP Implementation Plan

Hedef: `docs/revised-scope.md` §10'daki 12 kabul kriterinin tamamının **çalışır ve test edilmiş**
hâlde teslimi.

---

## 0. Repo yapısı

```
ticketanaliz/
├─ docs/
│  ├─ it-panel-srs.pdf                (kaynak, eski SRS)
│  ├─ ornek-ticket-maili.docx         (kaynak, gerçek mail)
│  ├─ Temmuz2026_Ticket_ERP.xlsx      (kaynak, kullanılmadı — bkz. open-questions G4)
│  ├─ revised-scope.md
│  ├─ email-parser-contract.md
│  ├─ open-questions.md
│  ├─ db-schema.md
│  └─ mvp-implementation-plan.md
├─ backend/
│  ├─ ItCockpit.sln
│  ├─ src/
│  │  ├─ ItCockpit.Domain/            entity + enum + domain kuralları, bağımlılıksız
│  │  ├─ ItCockpit.Application/       servisler, DTO, interface'ler, parser
│  │  ├─ ItCockpit.Infrastructure/    EF Core, Gmail, Hangfire, mock sağlayıcılar
│  │  └─ ItCockpit.Api/               controller, auth, DI, Swagger, Serilog
│  └─ tests/
│     └─ ItCockpit.Tests/             xUnit + FluentAssertions + fixtures
└─ frontend/                          React 18 + Vite + TS + MUI
```

**Modular monolith:** bağımlılık yönü tek yönlü — `Api → Infrastructure → Application → Domain`.
`Application` katmanı `IGmailTicketSource`, `IReminderMailSender`, `IAppSettingsProvider`,
`IClock`, `ICurrentUser` interface'lerini tanımlar; `Infrastructure` implemente eder.
Mikroservis ve message broker **yok**.

---

## Adım 1 — Backend iskeleti
- `dotnet new sln` + 4 sınıf kütüphanesi/webapi + 1 test projesi.
- Paketler: EF Core 8 (+SqlServer, +Design), Hangfire (+SqlServer, +AspNetCore),
  FluentValidation, Serilog.AspNetCore, Swashbuckle, Google.Apis.Gmail.v1,
  xUnit + FluentAssertions.
- `Directory.Build.props`: `net8.0`, `nullable enable`, `ImplicitUsings`.
  (`TreatWarningsAsErrors` **açılmadı** — EF/Hangfire kaynaklı üretilmiş kod gürültüsünü
  hata seviyesine çıkarmamak için. Build şu an zaten 0 uyarı veriyor.)

**Çıktı:** `dotnet build` temiz.

## Adım 2 — Domain
- Entity'ler (§db-schema), enum'lar: `TicketStatus`, `TicketType`, `WorkMode`,
  `ScheduleStatus`, `ReminderStatus`, `ParseWarningSeverity`.
- `TicketStatusTransitions` — geçiş matrisi domain'de, servis değil.
- `TicketNumber` value object: `^[IS]\d{6}_\d{6}$` doğrulaması + `TicketType` türetimi.
- `PersonNameNormalizer`: `"Turcan, Merve"` → `"Merve Turcan"` (tr-TR kültürü).

**Çıktı:** domain unit testleri yeşil.

## Adım 3 — Parser (`ItCockpit.Application/Parsing`)
- `RawTicketMail` / `ParsedTicket` / `TicketParseResult` / `ParseWarning` kayıtları.
- `ForwardEnvelopeExtractor` — en **içteki** forward bloğunu bulur, başlıkları çözer.
- `TicketMailParser` — kabul filtresi (F1–F4), subject regex, gövde alanları,
  description sınırları, uyarı üretimi.
- DB/Gmail bağımlılığı **yok**; `IClock` dışında hiçbir servis almaz.

**Çıktı:** `docs/ornek-ticket-maili.docx`'ten üretilen fixture ile 15+ assertion yeşil.

## Adım 4 — Persistence
- `AppDbContext` + `IEntityTypeConfiguration` sınıfları.
- Soft-delete global query filter, UTC dönüşüm converter'ı.
- `InitialCreate` migration → `localhost` / `ItManagerCockpit` veritabanına uygulama.
- Seed: 3 rol, 1 takım, 1 müdür (Ahmet Yılmaz) + 4 çalışan, `AppSettings` 8 anahtar,
  2026 Türkiye resmî tatilleri, 1 varsayılan hatırlatma şablonu.

**Çıktı:** `dotnet ef database update` başarılı, tablolar SQL Server'da doğrulanmış.

## Adım 5 — Ingestion + servisler
- `IGmailTicketSource` → `MockGmailTicketSource` (fixture'dan okur) ve `GmailTicketSource`
  (Gmail API, `label + from + subject` sorgusu, `historyId` artımlı).
- `TicketIngestionService` — parser çağrısı, 4 aşamalı duplicate kontrolü,
  `TicketMailSources` / `TicketParseWarnings` yazımı, `GmailSyncStates` güncellemesi.
- `TicketService` — atama, yeniden atama, durum geçişi (matris kontrollü), not.
- `DashboardService` — 8 metrik + 6 bölüm, aging eşikleri `AppSettings`'ten.
- `WorkScheduleService` — hafta oluşturma, gönderim, kilit, 3/2 kuralı ihlali,
  onay/red, manager override, "bugün kim nerede".
- `ReminderService` + `IReminderMailSender` → `MockReminderMailSender` / `GmailReminderMailSender`.
- `GmailIngestionJob` — Hangfire recurring, cron `AppSettings`/`appsettings.json`'dan.
- `AuditLogService` — tüm mutasyonlar.

**Çıktı:** servis unit testleri yeşil (in-memory / SQLite provider).

## Adım 6 — API
| Alan | Endpoint |
|------|----------|
| Auth | `POST /api/auth/mock-login`, `GET /api/auth/me`, `GET /api/auth/mock-users` |
| Dashboard | `GET /api/dashboard` |
| Tickets | `GET /api/tickets`, `GET /api/tickets/{id}`, `POST /{id}/assign`, `POST /{id}/status`, `POST /{id}/notes`, `GET /api/tickets/warnings` |
| Schedule | `GET/PUT /api/schedule/my-week`, `POST /api/schedule/my-week/submit`, `GET /api/schedule/team`, `GET /api/schedule/today`, `POST /api/schedule/{weekId}/decision`, `POST /api/schedule/{weekId}/override` |
| Reminders | `POST /api/reminders/preview`, `POST /api/reminders/send`, `GET /api/reminders/history`, `GET /api/reminders/templates` |
| Ingestion | `POST /api/ingestion/run` (manuel tetik), `GET /api/ingestion/state` |
| Settings | `GET/PUT /api/settings` |

- FluentValidation ile request doğrulama, `ProblemDetails` hata formatı.
- `MockAuthProvider`: `X-Mock-User` header / bearer token ile kullanıcı taklidi.
- Swagger `/swagger`.

**Çıktı:** API ayakta, Swagger erişilebilir, uçtan uca senaryo curl ile doğrulanmış.

## Adım 7 — Frontend
- Vite + React 18 + TS strict, MUI tema (`theme.ts` — Figma gelince tek dosyada değişir).
- Axios instance + interceptor (mock user header), TanStack Query, React Router v6.
- 14 reusable component (prompt §12).
- Sayfalar: `/login`, `/unauthorized`, `/error`, `/manager/dashboard`, `/manager/tickets`,
  `/manager/tickets/:id`, `/manager/team-schedule`, `/manager/reminders`,
  `/manager/reminder-history`, `/employee/my-schedule`.
- React Hook Form + Zod: atama formu, hatırlatma düzenleme, haftalık plan.
- Ticket detayında ve durum diyaloğunda **zorunlu uyarı metni**.

**Çıktı:** `npm run build` temiz, `tsc --noEmit` temiz.

## Adım 8 — Doğrulama
1. `dotnet build` (backend) — hatasız
2. `dotnet test` — tüm testler yeşil
3. `dotnet ef database update` — tablolar oluşmuş
4. `npm run build` (frontend) — hatasız
5. API ayağa kalkar → `POST /api/ingestion/run` → ticket oluşur
6. MVP 12 kriteri tek tek doğrulanır ve **gerçek çıktılarla** raporlanır

---

## Sıralama ve bağımlılıklar

```
1 iskelet ─► 2 domain ─► 3 parser ─────┐
                 └────► 4 persistence ─┴─► 5 servisler ─► 6 API ─► 8 doğrulama
                                                            └────► 7 frontend ─┘
```

## Kapsam dışı bırakılanlar (bilinçli)
- Gerçek Google OAuth akışı (client secret yok) — kod yazıldı, uçtan uca test edilemedi.
- Gerçek Gmail okuma/gönderme (credential yok) — `Mock*` implementasyonlar varsayılan.
- Docker / CI pipeline.
- Çalışan ticket ekranı (MVP'de zorunlu değil).
