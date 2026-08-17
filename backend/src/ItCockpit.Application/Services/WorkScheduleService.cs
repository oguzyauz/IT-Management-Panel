using System.Globalization;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Services;

public sealed class WorkScheduleService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAppSettingsProvider _settings;
    private readonly IAuditLogger _audit;

    public WorkScheduleService(
        IAppDbContext db, IClock clock, ICurrentUser currentUser,
        IAppSettingsProvider settings, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
    }

    public static DateOnly MondayOf(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Pazartesi = 0
        return date.AddDays(-diff);
    }

    /// <summary>Çalışanın varsayılan olarak doldurduğu hafta: <b>gelecek</b> hafta.</summary>
    public DateOnly DefaultTargetWeek() => MondayOf(_clock.Today).AddDays(7);

    public static IReadOnlyList<DateOnly> WeekDays(DateOnly monday) =>
        Enumerable.Range(0, 5).Select(monday.AddDays).ToList();

    public async Task<ScheduleRulesDto> GetRulesAsync(CancellationToken ct = default) => new(
        await _settings.GetIntAsync(AppSettingKeys.ScheduleRequiredOfficeDays, 3, ct),
        await _settings.GetIntAsync(AppSettingKeys.ScheduleRequiredHomeOfficeDays, 2, ct));

    /// <summary>
    /// Plan kilit anı: hedef haftadan <b>önceki</b> yapılandırılmış gün ve saat
    /// (varsayılan: hafta başlangıcından önceki Cuma 17:00 yerel).
    /// </summary>
    public async Task<DateTime> GetLockDeadlineUtcAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        var dayName = await _settings.GetStringAsync(AppSettingKeys.ScheduleLockDayOfWeek, "Friday", ct);
        var timeText = await _settings.GetStringAsync(AppSettingKeys.ScheduleLockTimeLocal, "17:00", ct);

        if (!Enum.TryParse<DayOfWeek>(dayName, ignoreCase: true, out var lockDay))
            lockDay = DayOfWeek.Friday;

        if (!TimeOnly.TryParse(timeText, CultureInfo.InvariantCulture, out var lockTime))
            lockTime = new TimeOnly(17, 0);

        // Hafta başlangıcından geriye doğru ilk 'lockDay'.
        var candidate = weekStart;
        do { candidate = candidate.AddDays(-1); } while (candidate.DayOfWeek != lockDay);

        var local = candidate.ToDateTime(lockTime);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _clock.LocalZone);
    }

    public async Task<MyWeekDto> GetMyWeekAsync(DateOnly? weekStart, CancellationToken ct = default)
    {
        var userId = RequireActor();
        return await GetWeekForUserAsync(userId, weekStart ?? DefaultTargetWeek(), ct);
    }

    public async Task<MyWeekDto> GetWeekForUserAsync(Guid userId, DateOnly weekStart, CancellationToken ct = default)
    {
        var monday = MondayOf(weekStart);
        var rules = await GetRulesAsync(ct);
        var deadline = await GetLockDeadlineUtcAsync(monday, ct);
        var holidays = await GetHolidaysAsync(monday, ct);

        var week = await _db.WorkScheduleWeeks.AsNoTracking()
            .Include(w => w.Days)
            .Include(w => w.Approvals)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == monday, ct);

        var decidedByIds = week?.Approvals.Select(a => a.DecidedByUserId).Distinct().ToList() ?? [];
        var decidedByNames = await _db.Users.AsNoTracking()
            .Where(u => decidedByIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var days = WeekDays(monday).Select(d =>
        {
            var stored = week?.Days.FirstOrDefault(x => x.Date == d);
            holidays.TryGetValue(d, out var holidayName);
            return new MyWeekDayDto(
                d, stored?.Mode, holidayName is not null, holidayName,
                stored?.IsManagerOverride ?? false, stored?.OverrideNote);
        }).ToList();

        return new MyWeekDto(
            week?.Id,
            monday,
            week?.Status ?? ScheduleStatus.Draft,
            _clock.UtcNow >= deadline,
            deadline,
            week?.HasRuleViolation ?? false,
            week?.RuleViolationNote,
            rules,
            days,
            week?.Approvals.OrderByDescending(a => a.DecidedAtUtc)
                .Select(a => new ScheduleDecisionDto(
                    a.Id, a.Decision, a.DecidedByUserId,
                    decidedByNames.TryGetValue(a.DecidedByUserId, out var n) ? n : "—",
                    a.DecidedAtUtc, a.Comment))
                .ToList() ?? []);
    }

    public async Task<MyWeekDto> SaveMyWeekAsync(SaveMyWeekRequest request, bool submit, CancellationToken ct = default)
    {
        var userId = RequireActor();
        var monday = MondayOf(request.WeekStartDate);
        var deadline = await GetLockDeadlineUtcAsync(monday, ct);
        var now = _clock.UtcNow;

        if (now >= deadline)
            throw new DomainRuleException("SCHEDULE_LOCKED",
                "Bu hafta için plan gönderim süresi doldu. Değişiklik için yöneticinize başvurun.");

        var validDays = WeekDays(monday).ToHashSet();
        foreach (var d in request.Days)
        {
            if (!validDays.Contains(d.Date))
                throw new DomainRuleException("DAY_OUT_OF_WEEK",
                    $"{d.Date:yyyy-MM-dd} tarihi {monday:yyyy-MM-dd} haftasının iş günlerinden biri değil.");
        }

        var week = await _db.WorkScheduleWeeks
            .Include(w => w.Days)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.WeekStartDate == monday, ct);

        if (week is null)
        {
            week = new WorkScheduleWeek
            {
                UserId = userId,
                WeekStartDate = monday,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.WorkScheduleWeeks.Add(week);
        }

        if (week.Status == ScheduleStatus.Approved)
            throw new DomainRuleException("ALREADY_APPROVED",
                "Onaylanmış plan çalışan tarafından değiştirilemez.");

        foreach (var incoming in request.Days)
        {
            var existing = week.Days.FirstOrDefault(x => x.Date == incoming.Date);
            if (existing is null)
            {
                week.Days.Add(new WorkScheduleDay
                {
                    WorkScheduleWeekId = week.Id,
                    Date = incoming.Date,
                    Mode = incoming.Mode
                });
            }
            else if (!existing.IsManagerOverride)
            {
                existing.Mode = incoming.Mode;
            }
        }

        var rules = await GetRulesAsync(ct);
        var holidays = await GetHolidaysAsync(monday, ct);
        ApplyRuleCheck(week, rules, holidays);

        week.UpdatedAtUtc = now;

        if (submit)
        {
            week.Status = ScheduleStatus.Submitted;
            week.SubmittedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(submit ? "SCHEDULE_SUBMIT" : "SCHEDULE_SAVE",
            nameof(WorkScheduleWeek), week.Id.ToString(),
            new { week.WeekStartDate, Days = request.Days }, ct);

        return await GetWeekForUserAsync(userId, monday, ct);
    }

    /// <summary>3 gün ofis / 2 gün home office kuralı. İhlal gönderimi <b>engellemez</b>, uyarı üretir.</summary>
    private static void ApplyRuleCheck(
        WorkScheduleWeek week, ScheduleRulesDto rules, IReadOnlyDictionary<DateOnly, string> holidays)
    {
        // Resmî tatiller kural dışıdır; yalnızca gerçek iş günleri değerlendirilir.
        var workingDays = WeekDays(week.WeekStartDate).Where(d => !holidays.ContainsKey(d)).ToList();

        var filled = workingDays
            .Select(d => week.Days.FirstOrDefault(x => x.Date == d))
            .Where(d => d is not null && Enum.IsDefined(d.Mode))
            .Select(d => d!)
            .ToList();

        var problems = new List<string>();

        var missing = workingDays.Count - filled.Count;
        if (missing > 0)
            problems.Add($"{missing} iş günü doldurulmamış.");

        var office = filled.Count(d => d.Mode == WorkMode.Office);
        var home = filled.Count(d => d.Mode == WorkMode.HomeOffice);
        var leave = filled.Count(d => d.Mode == WorkMode.Leave);

        // Tüm hafta izinliyse ofis/home office kuralı uygulanmaz.
        if (leave < workingDays.Count)
        {
            if (office < rules.RequiredOfficeDays)
                problems.Add($"Ofis günü sayısı {office}, beklenen en az {rules.RequiredOfficeDays}.");

            if (home > rules.RequiredHomeOfficeDays)
                problems.Add($"Home office günü sayısı {home}, beklenen en fazla {rules.RequiredHomeOfficeDays}.");
        }

        week.HasRuleViolation = problems.Count > 0;
        week.RuleViolationNote = problems.Count > 0 ? string.Join(" ", problems) : null;
    }

    public async Task<WeeklyScheduleMatrixDto> GetTeamMatrixAsync(DateOnly? weekStart, CancellationToken ct = default)
    {
        var monday = MondayOf(weekStart ?? _clock.Today);
        var days = WeekDays(monday);
        var holidays = await GetHolidaysAsync(monday, ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var weeks = await _db.WorkScheduleWeeks.AsNoTracking()
            .Include(w => w.Days)
            .Where(w => w.WeekStartDate == monday)
            .ToListAsync(ct);

        var rows = users.Select(u =>
        {
            var week = weeks.FirstOrDefault(w => w.UserId == u.Id);

            var cells = days.Select(d =>
            {
                var day = week?.Days.FirstOrDefault(x => x.Date == d);
                holidays.TryGetValue(d, out var holidayName);
                return new WeeklyScheduleCellDto(
                    d, day?.Mode, day?.IsManagerOverride ?? false, holidayName is not null, holidayName);
            }).ToList();

            return new WeeklyScheduleRowDto(
                u.Id, u.DisplayName, week?.Id,
                week?.Status ?? ScheduleStatus.Draft,
                week?.HasRuleViolation ?? false,
                week?.RuleViolationNote,
                cells);
        }).ToList();

        return new WeeklyScheduleMatrixDto(monday, days, rows);
    }

    public async Task<TodayTeamStatusDto> GetTodayStatusAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var monday = MondayOf(today);

        var holiday = await _db.WorkCalendar.AsNoTracking().FirstOrDefaultAsync(h => h.Date == today, ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var weeks = await _db.WorkScheduleWeeks.AsNoTracking()
            .Include(w => w.Days)
            .Where(w => w.WeekStartDate == monday)
            .ToListAsync(ct);

        var members = users.Select(u =>
        {
            var week = weeks.FirstOrDefault(w => w.UserId == u.Id);
            var day = week?.Days.FirstOrDefault(d => d.Date == today);
            var submitted = week is not null && week.Status != ScheduleStatus.Draft;
            return new TeamMemberDayStatusDto(u.Id, u.DisplayName, u.Title, day?.Mode, submitted);
        }).ToList();

        return new TodayTeamStatusDto(today, holiday is not null, holiday?.Name, members);
    }

    public async Task<MyWeekDto> DecideAsync(Guid weekId, ScheduleDecisionRequest request, CancellationToken ct = default)
    {
        var week = await _db.WorkScheduleWeeks.FirstOrDefaultAsync(w => w.Id == weekId, ct)
                   ?? throw new NotFoundException($"Çalışma planı bulunamadı: {weekId}");

        if (week.Status == ScheduleStatus.Draft)
            throw new DomainRuleException("NOT_SUBMITTED", "Henüz gönderilmemiş bir plan onaylanamaz veya reddedilemez.");

        var actorId = RequireActor();
        var now = _clock.UtcNow;

        week.Status = request.Decision == ScheduleDecision.Approved ? ScheduleStatus.Approved : ScheduleStatus.Rejected;
        week.UpdatedAtUtc = now;

        _db.WorkScheduleApprovals.Add(new WorkScheduleApproval
        {
            WorkScheduleWeekId = week.Id,
            Decision = request.Decision,
            DecidedByUserId = actorId,
            DecidedAtUtc = now,
            Comment = request.Comment
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SCHEDULE_DECISION", nameof(WorkScheduleWeek), week.Id.ToString(),
            new { request.Decision, request.Comment }, ct);

        return await GetWeekForUserAsync(week.UserId, week.WeekStartDate, ct);
    }

    public async Task<MyWeekDto> OverrideAsync(Guid weekId, ScheduleOverrideRequest request, CancellationToken ct = default)
    {
        var week = await _db.WorkScheduleWeeks
            .Include(w => w.Days)
            .FirstOrDefaultAsync(w => w.Id == weekId, ct)
            ?? throw new NotFoundException($"Çalışma planı bulunamadı: {weekId}");

        if (!WeekDays(week.WeekStartDate).Contains(request.Date))
            throw new DomainRuleException("DAY_OUT_OF_WEEK", "Tarih bu haftanın iş günlerinden biri değil.");

        var actorId = RequireActor();
        var now = _clock.UtcNow;

        var day = week.Days.FirstOrDefault(d => d.Date == request.Date);
        if (day is null)
        {
            day = new WorkScheduleDay { WorkScheduleWeekId = week.Id, Date = request.Date };
            week.Days.Add(day);
        }

        day.Mode = request.Mode;
        day.IsManagerOverride = true;
        day.OverriddenByUserId = actorId;
        day.OverrideNote = request.Note;

        var rules = await GetRulesAsync(ct);
        var holidays = await GetHolidaysAsync(week.WeekStartDate, ct);
        ApplyRuleCheck(week, rules, holidays);

        week.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SCHEDULE_OVERRIDE", nameof(WorkScheduleWeek), week.Id.ToString(),
            new { request.Date, request.Mode, request.Note }, ct);

        return await GetWeekForUserAsync(week.UserId, week.WeekStartDate, ct);
    }

    /// <summary>Bu hafta için planını hâlâ göndermemiş aktif çalışan sayısı.</summary>
    public async Task<int> CountMissingSubmissionsAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        var monday = MondayOf(weekStart);

        var activeUserIds = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var submitted = await _db.WorkScheduleWeeks.AsNoTracking()
            .Where(w => w.WeekStartDate == monday && w.Status != ScheduleStatus.Draft)
            .Select(w => w.UserId)
            .ToListAsync(ct);

        return activeUserIds.Except(submitted).Count();
    }

    private async Task<Dictionary<DateOnly, string>> GetHolidaysAsync(DateOnly monday, CancellationToken ct)
    {
        var end = monday.AddDays(6);
        return await _db.WorkCalendar.AsNoTracking()
            .Where(h => h.Date >= monday && h.Date <= end)
            .ToDictionaryAsync(h => h.Date, h => h.Name, ct);
    }

    private Guid RequireActor() =>
        _currentUser.UserId ?? throw new DomainRuleException("NO_ACTOR", "Oturum açmış kullanıcı bulunamadı.");
}
