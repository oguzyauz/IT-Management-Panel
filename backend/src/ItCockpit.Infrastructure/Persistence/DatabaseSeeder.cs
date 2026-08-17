using ItCockpit.Application.Services;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItCockpit.Infrastructure.Persistence;

/// <summary>
/// Geliştirme ve MVP demosu için temel veri. Idempotent — var olan kayıtları tekrar eklemez.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static readonly Guid ManagerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TeamId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly (Guid Id, string Email, string Name, string Title, string Role)[] SeedUsers =
    [
        (ManagerUserId, "ayilmaz@menarini.com.tr", "Ahmet Yılmaz", "IT Müdürü", RoleCodes.Manager),
        (Guid.Parse("33333333-3333-3333-3333-333333333301"), "doz@menarini.com.tr", "Dilara Öz", "Uygulama Destek Uzmanı", RoleCodes.Employee),
        (Guid.Parse("33333333-3333-3333-3333-333333333302"), "asecginli@menarini.com.tr", "Ayberk Seçginli", "ERP Destek Uzmanı", RoleCodes.Employee),
        (Guid.Parse("33333333-3333-3333-3333-333333333303"), "btufan@menarini.com.tr", "Beril Tufan", "Sistem Uzmanı", RoleCodes.Employee),
        (Guid.Parse("33333333-3333-3333-3333-333333333304"), "dkeydal@menarini.com.tr", "Duygu Keydal", "BT İş Zekası Destek Uzmanı", RoleCodes.Employee),
        (Guid.Parse("33333333-3333-3333-3333-333333333305"), "nozturk@menarini.com.tr", "Nazife Öztürk", "Uygulama Destek Uzmanı", RoleCodes.Employee),

        // Stajyer — kendi posta kutusu da okunuyor (bkz. Gmail:Mailboxes).
        (Guid.Parse("33333333-3333-3333-3333-333333333306"), "diloylay7@gmail.com", "Dilara", "Stajyer", RoleCodes.Employee)
    ];

    private static readonly (string Key, string Value, string Type, string Category, string Description)[] SeedSettings =
    [
        (AppSettingKeys.AgingStaleAfterDays, "2", "int", "Aging",
            "Kaç gündür güncellenmeyen ticket 'Güncelleme bekliyor' sayılır."),
        (AppSettingKeys.AgingOldAfterDays, "5", "int", "Aging",
            "Kaç gündür açık olan ticket 'Uzun süredir açık' sayılır."),
        (AppSettingKeys.AgingCriticalAfterDays, "7", "int", "Aging",
            "Kaç gündür açık olan ticket kritik seviyede 'Uzun süredir açık' sayılır."),
        (AppSettingKeys.ScheduleRequiredOfficeDays, "3", "int", "Schedule",
            "Haftalık asgari ofis günü sayısı."),
        (AppSettingKeys.ScheduleRequiredHomeOfficeDays, "2", "int", "Schedule",
            "Haftalık azami home office günü sayısı."),
        (AppSettingKeys.ScheduleLockDayOfWeek, "Friday", "string", "Schedule",
            "Plan gönderiminin kilitlendiği gün (hedef haftadan önceki)."),
        (AppSettingKeys.ScheduleLockTimeLocal, "17:00", "string", "Schedule",
            "Plan gönderiminin kilitlendiği yerel saat."),
        (AppSettingKeys.GmailPollIntervalMinutes, "5", "int", "Gmail",
            "Posta kutuları kaç dakikada bir otomatik okunsun.")
    ];

    /// <summary>
    /// 2026 Türkiye resmî tatilleri. Dinî bayram tarihleri hicri takvime bağlı olduğu için
    /// <b>doğrulanmalıdır</b> (bkz. docs/open-questions.md D3).
    /// </summary>
    private static readonly (string Date, string Name, WorkCalendarKind Kind)[] SeedHolidays2026 =
    [
        ("2026-01-01", "Yılbaşı", WorkCalendarKind.PublicHoliday),
        ("2026-03-19", "Ramazan Bayramı Arifesi", WorkCalendarKind.HalfDay),
        ("2026-03-20", "Ramazan Bayramı 1. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-03-21", "Ramazan Bayramı 2. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-03-22", "Ramazan Bayramı 3. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-04-23", "Ulusal Egemenlik ve Çocuk Bayramı", WorkCalendarKind.PublicHoliday),
        ("2026-05-01", "Emek ve Dayanışma Günü", WorkCalendarKind.PublicHoliday),
        ("2026-05-19", "Atatürk'ü Anma, Gençlik ve Spor Bayramı", WorkCalendarKind.PublicHoliday),
        ("2026-05-26", "Kurban Bayramı Arifesi", WorkCalendarKind.HalfDay),
        ("2026-05-27", "Kurban Bayramı 1. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-05-28", "Kurban Bayramı 2. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-05-29", "Kurban Bayramı 3. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-05-30", "Kurban Bayramı 4. Gün", WorkCalendarKind.PublicHoliday),
        ("2026-07-15", "Demokrasi ve Millî Birlik Günü", WorkCalendarKind.PublicHoliday),
        ("2026-08-30", "Zafer Bayramı", WorkCalendarKind.PublicHoliday),
        ("2026-10-28", "Cumhuriyet Bayramı Arifesi", WorkCalendarKind.HalfDay),
        ("2026-10-29", "Cumhuriyet Bayramı", WorkCalendarKind.PublicHoliday)
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await SeedRolesAsync(ct);
        await SeedTeamAndUsersAsync(now, ct);
        await SeedSettingsAsync(now, ct);
        await SeedHolidaysAsync(ct);
        await SeedReminderTemplateAsync(now, ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seed tamamlandı.");
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var existing = await _db.Roles.Select(r => r.Code).ToListAsync(ct);

        (int Id, string Code, string Name)[] roles =
        [
            (1, RoleCodes.Admin, "Sistem Yöneticisi"),
            (2, RoleCodes.Manager, "Yönetici"),
            (3, RoleCodes.Employee, "Çalışan")
        ];

        foreach (var (id, code, name) in roles)
        {
            if (!existing.Contains(code))
                _db.Roles.Add(new Role { Id = id, Code = code, Name = name });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedTeamAndUsersAsync(DateTime now, CancellationToken ct)
    {
        if (!await _db.Teams.AnyAsync(t => t.Id == TeamId, ct))
        {
            _db.Teams.Add(new Team
            {
                Id = TeamId,
                Name = "IT Uygulama Destek",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await _db.SaveChangesAsync(ct);
        }

        var roleIds = await _db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        var existingUserIds = await _db.Users.Select(u => u.Id).ToListAsync(ct);

        foreach (var (id, email, name, title, roleCode) in SeedUsers)
        {
            if (existingUserIds.Contains(id)) continue;

            _db.Users.Add(new User
            {
                Id = id,
                Email = email,
                DisplayName = name,
                Title = title,
                TeamId = TeamId,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            _db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleIds[roleCode] });
        }

        await _db.SaveChangesAsync(ct);

        var team = await _db.Teams.FirstAsync(t => t.Id == TeamId, ct);
        if (team.ManagerUserId is null)
        {
            team.ManagerUserId = ManagerUserId;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedSettingsAsync(DateTime now, CancellationToken ct)
    {
        var existing = await _db.AppSettings.Select(s => s.Key).ToListAsync(ct);

        foreach (var (key, value, type, category, description) in SeedSettings)
        {
            if (existing.Contains(key)) continue;

            _db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                DataType = type,
                Category = category,
                Description = description,
                UpdatedAtUtc = now
            });
        }
    }

    private async Task SeedHolidaysAsync(CancellationToken ct)
    {
        var existing = await _db.WorkCalendar.Select(h => h.Date).ToListAsync(ct);

        foreach (var (dateText, name, kind) in SeedHolidays2026)
        {
            var date = DateOnly.Parse(dateText);
            if (existing.Contains(date)) continue;

            _db.WorkCalendar.Add(new WorkCalendarEntry
            {
                Date = date,
                Name = name,
                Kind = kind,
                Year = date.Year
            });
        }
    }

    private async Task SeedReminderTemplateAsync(DateTime now, CancellationToken ct)
    {
        if (await _db.ReminderTemplates.AnyAsync(t => t.Code == "DEFAULT_TR", ct)) return;

        _db.ReminderTemplates.Add(new ReminderTemplate
        {
            Code = "DEFAULT_TR",
            Name = "Varsayılan hatırlatma (Türkçe)",
            SubjectTemplate = ReminderService.DefaultSubjectTemplate,
            BodyTemplate = ReminderService.DefaultBodyTemplate,
            IsDefault = true,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }
}
