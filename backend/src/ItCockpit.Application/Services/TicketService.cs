using System.Text.Json;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Services;

/// <summary>
/// Ticket takip işlemleri. <b>Hiçbir metot Tixbox'a yazmaz</b> — buradaki durum yalnızca
/// yönetim panelindeki takip durumudur.
/// </summary>
public sealed class TicketService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAppSettingsProvider _settings;
    private readonly IAuditLogger _audit;

    public TicketService(
        IAppDbContext db, IClock clock, ICurrentUser currentUser,
        IAppSettingsProvider settings, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
    }

    /// <summary>
    /// Müdür/yönetici tüm ticket'ları görür; çalışan yalnızca kendine atanmış olanları.
    /// Bu ayrım <b>sunucu tarafında</b> zorlanır — istemciye güvenilmez.
    /// </summary>
    private bool IsManager => _currentUser.IsInRole(RoleCodes.Manager) || _currentUser.IsInRole(RoleCodes.Admin);

    /// <summary>Çalışanın kendisine ait olmayan bir ticket'a dokunmasını engeller.</summary>
    private void EnsureCanAccess(Ticket ticket)
    {
        if (IsManager) return;

        if (ticket.AssigneeUserId is null || ticket.AssigneeUserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("Bu ticket size atanmamış.");
    }

    public async Task<AgingThresholds> GetThresholdsAsync(CancellationToken ct = default) => new(
        await _settings.GetIntAsync(AppSettingKeys.AgingStaleAfterDays, AgingThresholds.Default.StaleAfterDays, ct),
        await _settings.GetIntAsync(AppSettingKeys.AgingOldAfterDays, AgingThresholds.Default.OldAfterDays, ct),
        await _settings.GetIntAsync(AppSettingKeys.AgingCriticalAfterDays, AgingThresholds.Default.CriticalAfterDays, ct));

    public async Task<PagedResult<TicketListItemDto>> SearchAsync(TicketQuery query, CancellationToken ct = default)
    {
        var thresholds = await GetThresholdsAsync(ct);
        var now = _clock.UtcNow;

        // MailSources listede "hangi posta kutusundan okundu" rozetini beslemek için gerekli.
        var q = _db.Tickets.AsNoTracking()
            .Include(t => t.AssigneeUser)
            .Include(t => t.MailSources)
            .AsQueryable();

        // Çalışan için kapsam daraltması: istemci ne gönderirse göndersin yalnızca kendi kayıtları.
        if (!IsManager)
        {
            var self = RequireActor();
            q = q.Where(t => t.AssigneeUserId == self);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t =>
                EF.Functions.Like(t.ExternalTicketNumber, $"%{s}%") ||
                EF.Functions.Like(t.RequesterName, $"%{s}%") ||
                EF.Functions.Like(t.ApplicationName, $"%{s}%") ||
                EF.Functions.Like(t.Description, $"%{s}%") ||
                // "diloylay7" yazıp o kutudan okunanları süzebilmek için.
                t.MailSources.Any(m => EF.Functions.Like(m.SourceMailbox, $"%{s}%")));
        }

        if (query.Statuses is { Length: > 0 })
            q = q.Where(t => query.Statuses.Contains(t.Status));

        if (query.Unassigned == true)
            q = q.Where(t => t.AssigneeUserId == null);
        else if (query.AssigneeUserId is { } assignee)
            q = q.Where(t => t.AssigneeUserId == assignee);

        if (query.Priority is { } priority)
            q = q.Where(t => t.Priority == priority);

        if (!string.IsNullOrWhiteSpace(query.ApplicationName))
            q = q.Where(t => t.ApplicationName == query.ApplicationName);

        q = (query.SortBy, query.SortDescending) switch
        {
            ("Priority", true) => q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.OriginalSentAtUtc),
            ("Priority", false) => q.OrderBy(t => t.Priority).ThenByDescending(t => t.OriginalSentAtUtc),
            ("UpdatedAtUtc", true) => q.OrderByDescending(t => t.UpdatedAtUtc),
            ("UpdatedAtUtc", false) => q.OrderBy(t => t.UpdatedAtUtc),
            ("Status", true) => q.OrderByDescending(t => t.Status).ThenByDescending(t => t.OriginalSentAtUtc),
            ("Status", false) => q.OrderBy(t => t.Status).ThenByDescending(t => t.OriginalSentAtUtc),
            (_, false) => q.OrderBy(t => t.OriginalSentAtUtc),
            _ => q.OrderByDescending(t => t.OriginalSentAtUtc)
        };

        var warningTicketIds = await _db.TicketParseWarnings.AsNoTracking()
            .Where(w => w.TicketId != null && !w.IsAcknowledged && w.Severity >= ParseWarningSeverity.Warning)
            .Select(w => w.TicketId!.Value)
            .Distinct()
            .ToListAsync(ct);

        // Aging bellek üstünde hesaplanır; veri kümesi yönetici paneli ölçeğindedir.
        var all = await q.ToListAsync(ct);

        var projected = all
            .Select(t => Map(t, now, thresholds, warningTicketIds.Contains(t.Id)))
            .ToList();

        if (query.MinAging is { } minAging)
            projected = projected.Where(t => t.Aging >= minAging).ToList();

        var total = projected.Count;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = projected.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<TicketListItemDto>(items, total, page, pageSize);
    }

    public async Task<TicketDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var thresholds = await GetThresholdsAsync(ct);
        var now = _clock.UtcNow;

        var ticket = await _db.Tickets.AsNoTracking()
            .Include(t => t.AssigneeUser)
            .Include(t => t.Notes.Where(n => !n.IsDeleted)).ThenInclude(n => n.AuthorUser)
            .Include(t => t.StatusHistory).ThenInclude(h => h.ChangedByUser)
            .Include(t => t.Assignments).ThenInclude(a => a.AssignedToUser)
            .Include(t => t.Assignments).ThenInclude(a => a.AssignedByUser)
            .Include(t => t.MailSources)
            .Include(t => t.ParseWarnings)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Ticket bulunamadı: {id}");

        EnsureCanAccess(ticket);

        return new TicketDetailDto(
            ticket.Id,
            ticket.ExternalTicketNumber,
            ticket.TicketType,
            ticket.RequesterName,
            ticket.ApplicationName,
            ticket.Description,
            ticket.Priority,
            ticket.CategoryPath,
            ticket.ExternalReference,
            ticket.SourceRequestId,
            ticket.OriginalSentAtUtc,
            ticket.ExternalUrl,
            ticket.Status,
            // Mevcut durum menüden çıkarılır: matristeki Assigned -> Assigned girdisi
            // "yeniden atama" içindir, durum değiştirme değil (seçilirse SAME_STATUS döner).
            (IsManager
                    ? TicketStatusTransitions.AllowedFrom(ticket.Status)
                    : TicketStatusTransitions.AllowedFromForEmployee(ticket.Status))
                .Where(s => s != ticket.Status)
                .ToArray(),
            ticket.AssigneeUserId,
            ticket.AssigneeUser?.DisplayName,
            ticket.AutoAssigned,
            ticket.CreatedManually,
            ticket.AssignedAtUtc,
            ticket.CompletedAtUtc,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            AgingCalculator.DaysOpen(ticket.OriginalSentAtUtc, now),
            AgingCalculator.DaysSinceUpdate(ticket.UpdatedAtUtc, now),
            AgingCalculator.Evaluate(ticket.OriginalSentAtUtc, ticket.UpdatedAtUtc, now, thresholds),
            ticket.Notes.OrderByDescending(n => n.CreatedAtUtc)
                .Select(n => new TicketNoteDto(n.Id, n.AuthorUserId, n.AuthorUser.DisplayName, n.Body, n.CreatedAtUtc))
                .ToList(),
            ticket.StatusHistory.OrderByDescending(h => h.ChangedAtUtc)
                .Select(h => new TicketStatusHistoryDto(
                    h.Id, h.FromStatus, h.ToStatus, h.ChangedByUserId,
                    h.ChangedByUser?.DisplayName ?? "Sistem", h.ChangedAtUtc, h.Note))
                .ToList(),
            ticket.Assignments.OrderByDescending(a => a.AssignedAtUtc)
                .Select(a => new TicketAssignmentDto(
                    a.Id, a.AssignedToUserId, a.AssignedToUser.DisplayName,
                    a.AssignedByUserId, a.AssignedByUser?.DisplayName ?? "Sistem",
                    a.AssignedAtUtc, a.UnassignedAtUtc, a.Note))
                .ToList(),
            ticket.MailSources.OrderBy(m => m.OriginalSentAtUtc)
                .Select(m => new TicketMailSourceDto(
                    m.Id, m.SourceMailbox, m.GmailMessageId, m.Subject, m.OriginalSender,
                    DeserializeRecipients(m.OriginalRecipients), m.ForwardedBy, m.IsForwarded,
                    m.OriginalSentAtUtc, m.ReceivedAtUtc, m.IngestedAtUtc))
                .ToList(),
            ticket.ParseWarnings.OrderByDescending(w => w.Severity).ThenBy(w => w.Code)
                .Select(w => new ParseWarningDto(
                    w.Id, w.TicketId, ticket.ExternalTicketNumber, w.GmailMessageId, w.Code, w.Severity,
                    w.Message, w.FieldName, w.SubjectValue, w.BodyValue, w.IsAcknowledged, w.CreatedAtUtc))
                .ToList());
    }

    /// <summary>
    /// Panelden elle ticket açar. Mailden gelen kayıtlarla aynı kurallara tabidir:
    /// numara kanonik formatta olmalı ve daha önce kullanılmamış olmalıdır.
    /// </summary>
    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        if (!IsManager)
            throw new UnauthorizedAccessException("Elle ticket oluşturma yalnızca yönetici tarafından yapılabilir.");

        if (!TicketNumber.TryParse(request.ExternalTicketNumber, out var number))
            throw new DomainRuleException("INVALID_TICKET_NUMBER",
                "Ticket numarası I veya S ile başlamalı ve 'I260729_000144' biçiminde olmalıdır.");

        // Mail sonradan düşerse duplicate koruması aynı numarayı yakalar; burada da engellenir.
        if (await _db.Tickets.AnyAsync(t => t.ExternalTicketNumber == number.Value, ct))
            throw new DomainRuleException("DUPLICATE_TICKET",
                $"{number.Value} numaralı ticket zaten kayıtlı.");

        if (string.IsNullOrWhiteSpace(request.RequesterName))
            throw new DomainRuleException("REQUESTER_REQUIRED", "Talep eden kişi boş olamaz.");

        if (string.IsNullOrWhiteSpace(request.ApplicationName))
            throw new DomainRuleException("APPLICATION_REQUIRED", "Uygulama adı boş olamaz.");

        if (request.Priority is < 1 or > 5)
            throw new DomainRuleException("INVALID_PRIORITY", "Öncelik 1 ile 5 arasında olmalıdır.");

        var now = _clock.UtcNow;

        if (request.OriginalSentAtUtc > now.AddDays(1))
            throw new DomainRuleException("FUTURE_DATE", "Açılış tarihi gelecekte olamaz.");

        var actorId = RequireActor();

        User? assignee = null;
        if (request.AssigneeUserId is { } assigneeId)
        {
            assignee = await _db.Users.FirstOrDefaultAsync(u => u.Id == assigneeId && u.IsActive, ct)
                       ?? throw new NotFoundException($"Çalışan bulunamadı: {assigneeId}");
        }

        var ticket = new Ticket
        {
            ExternalTicketNumber = number.Value,
            TicketType = number.Type,
            RequesterName = PersonNameNormalizer.Normalize(request.RequesterName),
            ApplicationName = request.ApplicationName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Priority = request.Priority,
            CategoryPath = string.IsNullOrWhiteSpace(request.CategoryPath) ? null : request.CategoryPath.Trim(),
            ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim(),
            ExternalUrl = string.IsNullOrWhiteSpace(request.ExternalUrl) ? null : request.ExternalUrl.Trim(),
            OriginalSentAtUtc = request.OriginalSentAtUtc,
            CreatedManually = true,
            Status = assignee is null ? TicketStatus.Unassigned : TicketStatus.Assigned,
            AssigneeUserId = assignee?.Id,
            AssignedAtUtc = assignee is null ? null : now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Tickets.Add(ticket);

        _db.TicketStatusHistory.Add(new TicketStatusHistory
        {
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = ticket.Status,
            ChangedByUserId = actorId,
            ChangedAtUtc = now,
            Note = "Panelden elle oluşturuldu"
        });

        if (assignee is not null)
        {
            _db.TicketAssignments.Add(new TicketAssignment
            {
                TicketId = ticket.Id,
                AssignedToUserId = assignee.Id,
                AssignedByUserId = actorId,
                AssignedAtUtc = now,
                Note = "Elle oluştururken atandı"
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TICKET_CREATE_MANUAL", nameof(Ticket), ticket.Id.ToString(),
            new { ticket.ExternalTicketNumber, ticket.RequesterName, ticket.Priority }, ct);

        return await GetAsync(ticket.Id, ct);
    }

    public async Task<TicketDetailDto> AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken ct = default)
    {
        // Atama her koşulda yöneticiye aittir; çalışan kendi işini başkasına devredemez.
        if (!IsManager)
            throw new UnauthorizedAccessException("Ticket atama yalnızca yönetici tarafından yapılabilir.");

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                     ?? throw new NotFoundException($"Ticket bulunamadı: {ticketId}");

        var assignee = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.AssigneeUserId && u.IsActive, ct)
                       ?? throw new NotFoundException($"Çalışan bulunamadı: {request.AssigneeUserId}");

        var actorId = RequireActor();
        var now = _clock.UtcNow;

        if (ticket.AssigneeUserId == assignee.Id)
            throw new DomainRuleException("ALREADY_ASSIGNED", "Ticket zaten bu çalışana atanmış.");

        var targetStatus = ticket.Status is TicketStatus.InProgress ? TicketStatus.InProgress : TicketStatus.Assigned;

        if (ticket.Status != targetStatus && !TicketStatusTransitions.IsAllowed(ticket.Status, targetStatus))
            throw new DomainRuleException("INVALID_TRANSITION",
                $"'{ticket.Status}' durumundaki bir ticket atanamaz.");

        var openAssignment = await _db.TicketAssignments
            .Where(a => a.TicketId == ticket.Id && a.UnassignedAtUtc == null)
            .ToListAsync(ct);

        foreach (var a in openAssignment) a.UnassignedAtUtc = now;

        var previousAssignee = ticket.AssigneeUserId;
        var previousStatus = ticket.Status;

        ticket.AssigneeUserId = assignee.Id;
        ticket.AssignedAtUtc = now;
        ticket.UpdatedAtUtc = now;
        ticket.Status = targetStatus;

        _db.TicketAssignments.Add(new TicketAssignment
        {
            TicketId = ticket.Id,
            AssignedToUserId = assignee.Id,
            AssignedByUserId = actorId,
            AssignedAtUtc = now,
            Note = request.Note
        });

        if (previousStatus != targetStatus)
        {
            _db.TicketStatusHistory.Add(new TicketStatusHistory
            {
                TicketId = ticket.Id,
                FromStatus = previousStatus,
                ToStatus = targetStatus,
                ChangedByUserId = actorId,
                ChangedAtUtc = now,
                Note = request.Note
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(previousAssignee is null ? "TICKET_ASSIGN" : "TICKET_REASSIGN",
            nameof(Ticket), ticket.Id.ToString(),
            new { From = previousAssignee, To = assignee.Id, request.Note }, ct);

        return await GetAsync(ticket.Id, ct);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(Guid ticketId, ChangeStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                     ?? throw new NotFoundException($"Ticket bulunamadı: {ticketId}");

        EnsureCanAccess(ticket);
        var actorId = RequireActor();

        if (ticket.Status == request.Status)
            throw new DomainRuleException("SAME_STATUS", "Ticket zaten bu durumda.");

        if (!TicketStatusTransitions.IsAllowed(ticket.Status, request.Status))
            throw new DomainRuleException("INVALID_TRANSITION",
                $"'{ticket.Status}' durumundan '{request.Status}' durumuna geçilemez.");

        // Çalışan yalnızca işi üstlenip tamamlayabilir; atamayı geri verme ve arşivleme müdürde.
        if (!IsManager && !TicketStatusTransitions.IsAllowedForEmployee(ticket.Status, request.Status))
            throw new DomainRuleException("EMPLOYEE_TRANSITION_NOT_ALLOWED",
                $"'{request.Status}' durumuna yalnızca yönetici geçirebilir.");

        if (request.Status is TicketStatus.InProgress or TicketStatus.Completed && ticket.AssigneeUserId is null)
            throw new DomainRuleException("ASSIGNEE_REQUIRED",
                "Ticket bir çalışana atanmadan bu duruma alınamaz.");

        var now = _clock.UtcNow;
        var from = ticket.Status;

        ticket.Status = request.Status;
        ticket.UpdatedAtUtc = now;
        ticket.CompletedAtUtc = request.Status == TicketStatus.Completed ? now
            : request.Status == TicketStatus.Archived ? ticket.CompletedAtUtc
            : null;

        if (request.Status == TicketStatus.Unassigned)
        {
            ticket.AssigneeUserId = null;
            ticket.AssignedAtUtc = null;

            var open = await _db.TicketAssignments
                .Where(a => a.TicketId == ticket.Id && a.UnassignedAtUtc == null).ToListAsync(ct);
            foreach (var a in open) a.UnassignedAtUtc = now;
        }

        _db.TicketStatusHistory.Add(new TicketStatusHistory
        {
            TicketId = ticket.Id,
            FromStatus = from,
            ToStatus = request.Status,
            ChangedByUserId = actorId,
            ChangedAtUtc = now,
            Note = request.Note
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TICKET_STATUS_CHANGE", nameof(Ticket), ticket.Id.ToString(),
            new { From = from, To = request.Status, request.Note }, ct);

        return await GetAsync(ticket.Id, ct);
    }

    public async Task<TicketNoteDto> AddNoteAsync(Guid ticketId, AddNoteRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                     ?? throw new NotFoundException($"Ticket bulunamadı: {ticketId}");

        EnsureCanAccess(ticket);

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new DomainRuleException("EMPTY_NOTE", "Not boş olamaz.");

        var actorId = RequireActor();
        var now = _clock.UtcNow;

        var note = new TicketNote
        {
            TicketId = ticket.Id,
            AuthorUserId = actorId,
            Body = request.Body.Trim(),
            CreatedAtUtc = now
        };

        _db.TicketNotes.Add(note);
        ticket.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("TICKET_NOTE_ADD", nameof(Ticket), ticket.Id.ToString(), null, ct);

        var author = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId, ct);
        return new TicketNoteDto(note.Id, actorId, author.DisplayName, note.Body, note.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ParseWarningDto>> GetWarningsAsync(bool onlyUnacknowledged, CancellationToken ct = default)
    {
        var q = _db.TicketParseWarnings.AsNoTracking().Include(w => w.Ticket).AsQueryable();
        if (onlyUnacknowledged) q = q.Where(w => !w.IsAcknowledged);

        return await q
            .OrderByDescending(w => w.Severity).ThenByDescending(w => w.CreatedAtUtc)
            .Take(200)
            .Select(w => new ParseWarningDto(
                w.Id, w.TicketId, w.Ticket != null ? w.Ticket.ExternalTicketNumber : null,
                w.GmailMessageId, w.Code, w.Severity, w.Message, w.FieldName,
                w.SubjectValue, w.BodyValue, w.IsAcknowledged, w.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task AcknowledgeWarningAsync(Guid warningId, CancellationToken ct = default)
    {
        var warning = await _db.TicketParseWarnings.FirstOrDefaultAsync(w => w.Id == warningId, ct)
                      ?? throw new NotFoundException($"Uyarı bulunamadı: {warningId}");

        warning.IsAcknowledged = true;
        warning.AcknowledgedByUserId = RequireActor();
        warning.AcknowledgedAtUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PARSE_WARNING_ACK", nameof(TicketParseWarning), warningId.ToString(), null, ct);
    }

    internal static TicketListItemDto Map(Ticket t, DateTime now, AgingThresholds thresholds, bool hasWarning) =>
        new(
            t.Id, t.ExternalTicketNumber, t.TicketType, t.RequesterName, t.ApplicationName,
            t.Priority, t.Status, t.AssigneeUserId, t.AssigneeUser?.DisplayName,
            t.OriginalSentAtUtc, t.UpdatedAtUtc,
            AgingCalculator.DaysOpen(t.OriginalSentAtUtc, now),
            AgingCalculator.DaysSinceUpdate(t.UpdatedAtUtc, now),
            AgingCalculator.Evaluate(t.OriginalSentAtUtc, t.UpdatedAtUtc, now, thresholds),
            hasWarning, t.AutoAssigned, t.CreatedManually,
            // MailSources Include edilmediyse boş gelir; elle eklenen kayıtlarda zaten boştur.
            t.MailSources
                .Select(m => m.SourceMailbox)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            t.ExternalUrl);

    private static IReadOnlyList<string> DeserializeRecipients(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private Guid RequireActor() =>
        _currentUser.UserId ?? throw new DomainRuleException("NO_ACTOR", "Oturum açmış kullanıcı bulunamadı.");
}
