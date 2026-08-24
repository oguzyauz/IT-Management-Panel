using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Services;

public sealed class DashboardService
{
    private const int SectionLimit = 10;

    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly TicketService _tickets;
    private readonly WorkScheduleService _schedule;
    private readonly ReminderService _reminders;

    public DashboardService(
        IAppDbContext db, IClock clock, TicketService tickets,
        WorkScheduleService schedule, ReminderService reminders)
    {
        _db = db;
        _clock = clock;
        _tickets = tickets;
        _schedule = schedule;
        _reminders = reminders;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var today = _clock.Today;
        var thresholds = await _tickets.GetThresholdsAsync(ct);

        var openStatuses = new[]
        {
            TicketStatus.New, TicketStatus.Unassigned, TicketStatus.Assigned, TicketStatus.InProgress
        };

        var openTickets = await _db.Tickets.AsNoTracking()
            .Include(t => t.AssigneeUser)
            .Include(t => t.MailSources)
            .Where(t => openStatuses.Contains(t.Status))
            .ToListAsync(ct);

        var warningTicketIds = await _db.TicketParseWarnings.AsNoTracking()
            .Where(w => w.TicketId != null && !w.IsAcknowledged && w.Severity >= ParseWarningSeverity.Warning)
            .Select(w => w.TicketId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var mapped = openTickets
            .Select(t => TicketService.Map(t, now, thresholds, warningTicketIds.Contains(t.Id)))
            .ToList();

        var todayStatus = await _schedule.GetTodayStatusAsync(ct);
        var currentWeek = WorkScheduleService.MondayOf(today);
        var missingSubmissions = await _schedule.CountMissingSubmissionsAsync(currentWeek.AddDays(7), ct);

        var metrics = new DashboardMetricsDto(
            TotalOpenTickets: mapped.Count,
            UnassignedTickets: mapped.Count(t => t.AssigneeUserId is null),
            InProgressTickets: mapped.Count(t => t.Status == TicketStatus.InProgress),
            StaleTickets: mapped.Count(t => t.Aging >= AgingLevel.NeedsUpdate),
            InOfficeToday: todayStatus.Members.Count(m => m.Mode == WorkMode.Office),
            HomeOfficeToday: todayStatus.Members.Count(m => m.Mode == WorkMode.HomeOffice),
            OnLeaveToday: todayStatus.Members.Count(m => m.Mode == WorkMode.Leave),
            MissingScheduleSubmissions: missingSubmissions);

        var unassigned = mapped
            .Where(t => t.AssigneeUserId is null)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.OriginalSentAtUtc)
            .Take(SectionLimit)
            .ToList();

        var attention = mapped
            .Where(t => t.Aging >= AgingLevel.NeedsUpdate || t.HasParseWarning)
            .OrderByDescending(t => t.Aging)
            .ThenBy(t => t.Priority)
            .ThenBy(t => t.OriginalSentAtUtc)
            .Take(SectionLimit)
            .ToList();

        var todayModeByUser = todayStatus.Members.ToDictionary(m => m.UserId, m => m.Mode);

        var workload = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.Title })
            .ToListAsync(ct);

        var workloadDtos = workload.Select(u =>
        {
            var own = mapped.Where(t => t.AssigneeUserId == u.Id).ToList();
            return new EmployeeWorkloadDto(
                u.Id, u.DisplayName, u.Title,
                own.Count,
                own.Count(t => t.Status == TicketStatus.InProgress),
                own.Count(t => t.Aging >= AgingLevel.NeedsUpdate),
                todayModeByUser.TryGetValue(u.Id, out var mode) ? mode : null);
        })
        .OrderByDescending(w => w.OpenTicketCount)
        .ThenBy(w => w.DisplayName)
        .ToList();

        var matrix = await _schedule.GetTeamMatrixAsync(currentWeek, ct);
        var recentReminders = await _reminders.GetHistoryAsync(new ReminderHistoryQuery(Take: SectionLimit), ct);

        var mismatchWarnings = await _db.TicketParseWarnings.AsNoTracking()
            .Include(w => w.Ticket)
            .Where(w => !w.IsAcknowledged && w.Severity == ParseWarningSeverity.Error)
            .OrderByDescending(w => w.CreatedAtUtc)
            .Take(SectionLimit)
            .Select(w => new ParseWarningDto(
                w.Id, w.TicketId, w.Ticket != null ? w.Ticket.ExternalTicketNumber : null,
                w.GmailMessageId, w.Code, w.Severity, w.Message, w.FieldName,
                w.SubjectValue, w.BodyValue, w.IsAcknowledged, w.CreatedAtUtc))
            .ToListAsync(ct);

        // Çalışanların kendi ticket'larında yaptığı güncellemeler müdüre burada düşer.
        // Müdürün kendi işlemleri ve sistemin (mail ile oluşturma) kayıtları hariç tutulur.
        var managerIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Code == RoleCodes.Manager || ur.Role.Code == RoleCodes.Admin)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var recentTeamUpdates = await _db.TicketStatusHistory.AsNoTracking()
            .Include(h => h.Ticket)
            .Include(h => h.ChangedByUser)
            .Where(h => h.ChangedByUserId != null && !managerIds.Contains(h.ChangedByUserId!.Value))
            .OrderByDescending(h => h.ChangedAtUtc)
            .Take(SectionLimit)
            .Select(h => new TeamStatusUpdateDto(
                h.TicketId,
                h.Ticket.ExternalTicketNumber,
                h.Ticket.ApplicationName,
                h.ChangedByUserId!.Value,
                h.ChangedByUser!.DisplayName,
                h.FromStatus,
                h.ToStatus,
                h.ChangedAtUtc,
                h.Note))
            .ToListAsync(ct);

        return new DashboardDto(
            metrics, todayStatus, unassigned, attention, workloadDtos,
            matrix, recentReminders, mismatchWarnings, recentTeamUpdates, thresholds.ToDto());
    }
}
