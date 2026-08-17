# Database Schema — SQL Server

DB adı: `ItManagerCockpit` · Collation: `Turkish_CI_AS` · EF Core 8 Code-First

Tüm zaman alanları **UTC**'dir (`datetime2(3)`). Adlandırma: tablo çoğul, PK `Id`,
FK `<Entity>Id`. Soft delete kullanılan tablolarda `IsDeleted bit` + global query filter.
`AuditLogs` **hiçbir koşulda** silinmez.

---

## 1. Kimlik ve organizasyon

### Users
| Kolon | Tip | Not |
|-------|-----|-----|
| Id | uniqueidentifier | PK |
| Email | nvarchar(256) | **UQ**, filtered (IsDeleted=0) |
| DisplayName | nvarchar(200) | |
| Title | nvarchar(200) | null |
| TeamId | uniqueidentifier | FK → Teams, null |
| IsActive | bit | default 1 — **devre dışı bırakma mekanizması** |
| CreatedAtUtc / UpdatedAtUtc | datetime2(3) | |

> **Users'ta soft delete yoktur.** Atama, durum geçmişi ve denetim kayıtları kullanıcıya
> zorunlu FK ile bağlıdır; `Users` üzerinde global query filter kullanmak bu tarihsel kayıtların
> sessizce kaybolmasına yol açar (EF Core bu durumu uyarı olarak bildirir). Kullanıcı ayrılışı
> `IsActive = 0` ile yönetilir.

### Roles
`Id (int, PK)`, `Code nvarchar(50) UQ` (`ADMIN`/`MANAGER`/`EMPLOYEE`), `Name nvarchar(100)`

### UserRoles
`UserId + RoleId` composite PK. FK cascade → Users, restrict → Roles.

### Teams
`Id`, `Name nvarchar(200) UQ`, `ManagerUserId (FK → Users, null)`, `IsDeleted`, timestamps.

---

## 2. Ticket çekirdeği

### Tickets
| Kolon | Tip | Not |
|-------|-----|-----|
| Id | uniqueidentifier | PK |
| ExternalTicketNumber | nvarchar(20) | **UQ** (filtered IsDeleted=0) — duplicate anahtarı #2 |
| TicketType | nvarchar(30) | `INCIDENT` \| `SERVICE_REQUEST` |
| RequesterName | nvarchar(200) | normalize edilmiş ("Merve Turcan") |
| ApplicationName | nvarchar(200) | "ERP TR" |
| Description | nvarchar(max) | serbest metin |
| Priority | int | 1–5 |
| CategoryPath | nvarchar(500) | null |
| ExternalReference | nvarchar(200) | null (`N/A` → null) |
| SourceRequestId | nvarchar(50) | null, IX — duplicate anahtarı #3 |
| OriginalSentAtUtc | datetime2(3) | **forward'ın iç tarihi** |
| ExternalUrl | nvarchar(1000) | Tixbox derin bağlantısı, null |
| Status | nvarchar(30) | bkz. durum makinesi |
| AssigneeUserId | uniqueidentifier | FK → Users, **null** (gruba gelen mailde null) |
| AutoAssigned | bit | Kişiye özel mail olduğu için sistem atadı — müdür ataması değil |
| AssignedAtUtc | datetime2(3) | null |
| CompletedAtUtc | datetime2(3) | null |
| CreatedAtUtc / UpdatedAtUtc | datetime2(3) | |
| IsDeleted | bit | |

**İndeksler**
- `UQ_Tickets_ExternalTicketNumber` — filtered `WHERE IsDeleted = 0`
- `IX_Tickets_Status_OriginalSentAtUtc` — dashboard aging sorguları
- `IX_Tickets_AssigneeUserId_Status` — çalışan bazlı iş yükü
- `IX_Tickets_SourceRequestId` — filtered `WHERE SourceRequestId IS NOT NULL`
- `IX_Tickets_UpdatedAtUtc` — "güncelleme bekliyor" sorgusu

**Durum makinesi**

```
            (mail düşer)
                 │
                 ▼
            UNASSIGNED ──assign──► ASSIGNED ──start──► IN_PROGRESS
                 ▲                    │                    │
                 │                    └──complete──────────┤
              reassign                                     ▼
                                                      COMPLETED
                                                           │
                                                        archive
                                                           ▼
                                                       ARCHIVED
```

`NEW` durumu enum'da tanımlıdır ancak ingestion **doğrudan `UNASSIGNED`** yazar
(prompt §7 gereği). `NEW`, ileride gelebilecek "okundu ama işlenmedi" senaryosu için ayrılmıştır.

İzin verilen geçişler (`TicketStatusTransitions`):
`UNASSIGNED→ASSIGNED`, `ASSIGNED→{IN_PROGRESS, UNASSIGNED, ASSIGNED(reassign), COMPLETED}`,
`IN_PROGRESS→{COMPLETED, ASSIGNED}`, `COMPLETED→{ARCHIVED, IN_PROGRESS(geri al)}`,
`ARCHIVED→COMPLETED`.

### TicketAssignments (atama geçmişi)
`Id`, `TicketId (FK, cascade)`, `AssignedToUserId (FK)`,
`AssignedByUserId (FK, **null** = sistem otomatik ataması)`,
`AssignedAtUtc`, `UnassignedAtUtc (null)`, `Note nvarchar(500)`.
IX: `TicketId, AssignedAtUtc DESC`.

### TicketStatusHistory
`Id`, `TicketId (FK, cascade)`, `FromStatus nvarchar(30) null`, `ToStatus nvarchar(30)`,
`ChangedByUserId (FK, **null** = sistem)`, `ChangedAtUtc`, `Note nvarchar(500)`.

> Mail ile otomatik oluşturma sırasında gerçek bir aktör olmadığı için `ChangedByUserId`
> `null` bırakılır; sahte bir "system" kullanıcısı seed edilmez.

### TicketNotes (dahili not)
`Id`, `TicketId (FK, cascade)`, `AuthorUserId (FK)`, `Body nvarchar(max)`,
`CreatedAtUtc`, `IsDeleted`.

### TicketMailSources
Aynı ticket'a ait **her** mail (ilk + forward'lar) burada izlenir.

| Kolon | Tip | Not |
|-------|-----|-----|
| Id | uniqueidentifier | PK |
| TicketId | uniqueidentifier | FK cascade |
| SourceMailbox | nvarchar(320) | IX — bu mailin okunduğu posta kutusu |
| GmailMessageId | nvarchar(100) | **UQ** — duplicate anahtarı #1 |
| GmailThreadId | nvarchar(100) | IX |
| Subject | nvarchar(500) | duplicate anahtarı #4 (OriginalSentAtUtc ile) |
| OriginalSender | nvarchar(320) | |
| OriginalRecipients | nvarchar(max) | JSON dizi |
| ForwardedBy | nvarchar(320) | null — dış zarf göndereni |
| IsForwarded | bit | |
| OriginalSentAtUtc | datetime2(3) | |
| ReceivedAtUtc | datetime2(3) | Gmail internalDate |
| IngestedAtUtc | datetime2(3) | |

IX: `(Subject, OriginalSentAtUtc)` — duplicate anahtarı #4.

### TicketParseWarnings
`Id`, `TicketId (FK cascade, null olabilir — reddedilen mailler için)`,
`GmailMessageId nvarchar(100)`, `Code nvarchar(50)`, `Severity nvarchar(20)`,
`Message nvarchar(1000)`, `FieldName nvarchar(100) null`,
`SubjectValue nvarchar(500) null`, `BodyValue nvarchar(500) null`,
`IsAcknowledged bit`, `AcknowledgedByUserId (FK null)`, `AcknowledgedAtUtc`, `CreatedAtUtc`.
IX: `IsAcknowledged, Severity`.

---

## 3. Hibrit çalışma takvimi

### WorkScheduleWeeks
| Kolon | Tip | Not |
|-------|-----|-----|
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK |
| WeekStartDate | date | **pazartesi** |
| Status | nvarchar(20) | `DRAFT`/`SUBMITTED`/`APPROVED`/`REJECTED` |
| SubmittedAtUtc / LockedAtUtc | datetime2(3) | null |
| HasRuleViolation | bit | 3 ofis / 2 HO kuralı |
| RuleViolationNote | nvarchar(500) | null |
| CreatedAtUtc / UpdatedAtUtc | | |

**UQ:** `(UserId, WeekStartDate)`

### WorkScheduleDays
`Id`, `WorkScheduleWeekId (FK cascade)`, `Date date`, `Mode nvarchar(20)`
(`OFFICE`/`HOME_OFFICE`/`LEAVE`), `IsManagerOverride bit`,
`OverriddenByUserId (FK null)`, `OverrideNote nvarchar(500)`.
**UQ:** `(WorkScheduleWeekId, Date)` · IX: `(Date, Mode)` — "bugün kim nerede" sorgusu.

### WorkScheduleApprovals
`Id`, `WorkScheduleWeekId (FK cascade)`, `Decision nvarchar(20)` (`APPROVED`/`REJECTED`),
`DecidedByUserId (FK)`, `DecidedAtUtc`, `Comment nvarchar(1000)`.

### WorkCalendar (resmî tatil / özel gün)
`Id (int identity)`, `Date date UQ`, `Name nvarchar(200)`,
`Kind nvarchar(20)` (`PUBLIC_HOLIDAY`/`HALF_DAY`/`COMPANY_HOLIDAY`), `Year int (IX)`.

---

## 4. Hatırlatma

### ReminderTemplates
`Id`, `Code nvarchar(50) UQ`, `Name nvarchar(200)`, `SubjectTemplate nvarchar(500)`,
`BodyTemplate nvarchar(max)`, `IsDefault bit`, `IsActive bit`, timestamps.

Placeholder'lar: `{{AssigneeName}}`, `{{ManagerName}}`, `{{TicketCount}}`, `{{TicketList}}`,
`{{Date}}`.

### ReminderDeliveries
`Id`, `RecipientUserId (FK)`, `SentByUserId (FK)`, `TemplateId (FK null)`,
`Subject nvarchar(500)`, `Body nvarchar(max)`, `TicketIdsJson nvarchar(max)`,
`CcJson nvarchar(max) null`, `Status nvarchar(20)` (`PENDING`/`SENT`/`FAILED`),
`ProviderMessageId nvarchar(200) null`, `ErrorMessage nvarchar(2000) null`,
`CreatedAtUtc`, `SentAtUtc null`.
IX: `(RecipientUserId, CreatedAtUtc DESC)`, `(Status)`.

---

## 5. Sistem

### AuditLogs (asla silinmez)
`Id (bigint identity)`, `OccurredAtUtc`, `ActorUserId (FK null)`, `ActorEmail nvarchar(256)`,
`Action nvarchar(100)`, `EntityType nvarchar(100)`, `EntityId nvarchar(100)`,
`ChangesJson nvarchar(max)`, `IpAddress nvarchar(64)`, `CorrelationId nvarchar(64)`.
IX: `(EntityType, EntityId)`, `(OccurredAtUtc DESC)`.

### AppSettings
`Key nvarchar(200) PK`, `Value nvarchar(max)`, `DataType nvarchar(20)`,
`Category nvarchar(50)`, `Description nvarchar(500)`, `UpdatedAtUtc`, `UpdatedByUserId (FK null)`.

Seed edilen anahtarlar:

| Key | Default | Kategori |
|-----|---------|----------|
| `Aging.StaleAfterDays` | `2` | Aging |
| `Aging.OldAfterDays` | `5` | Aging |
| `Aging.CriticalAfterDays` | `7` | Aging |
| `Schedule.RequiredOfficeDays` | `3` | Schedule |
| `Schedule.RequiredHomeOfficeDays` | `2` | Schedule |
| `Schedule.LockDayOfWeek` | `Friday` | Schedule |
| `Schedule.LockTimeLocal` | `17:00` | Schedule |
| `Gmail.PollIntervalMinutes` | `5` | Gmail |

### GmailSyncStates
`Id (int identity)`, `MailboxAddress nvarchar(320) UQ`, `LastHistoryId nvarchar(50) null`,
`LastSyncStartedAtUtc`, `LastSyncCompletedAtUtc`, `LastSyncStatus nvarchar(20)`,
`LastError nvarchar(2000)`, `MessagesSeen int`, `TicketsCreated int`, `DuplicatesSkipped int`,
`MailsRejected int`.

---

## 6. Tablo sayısı

**19 iş tablosu:** Users, Roles, UserRoles, Teams, Tickets, TicketAssignments, TicketStatusHistory,
TicketNotes, TicketMailSources, TicketParseWarnings, WorkScheduleWeeks, WorkScheduleDays,
WorkScheduleApprovals, WorkCalendar, ReminderTemplates, ReminderDeliveries, AuditLogs,
AppSettings, GmailSyncStates.

Bunlara ek olarak EF'in `__EFMigrationsHistory` tablosu ve Hangfire'ın çalışma anında kendi
oluşturduğu `HangFire` şeması bulunur.

## 7. Silme politikası

| Tablo | Politika |
|-------|----------|
| Teams, Tickets, TicketNotes | Soft delete (`IsDeleted` + global query filter) |
| Users | Soft delete **yok** — `IsActive = 0` ile devre dışı bırakılır |
| TicketAssignments, TicketStatusHistory, TicketMailSources, TicketParseWarnings | Ticket ile cascade + Ticket'ın soft-delete filtresiyle eşleşen query filter |
| AuditLogs | **Silinmez** — hiçbir yol yok |
| ReminderDeliveries | Silinmez (gönderim kanıtı) |
