using System.Globalization;
using System.Text;
using System.Text.Json;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItCockpit.Application.Services;

public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    public string Provider { get; set; } = "Mock";   // Mock | Google
    public string FromAddress { get; set; } = "ayilmaz@menarini.com.tr";
}

/// <summary>
/// Hatırlatma maili akışı: önizleme -&gt; müdür düzenler -&gt; <b>açık onay</b> -&gt; gönderim -&gt; log.
/// Onay olmadan hiçbir mail gönderilmez.
/// </summary>
public sealed class ReminderService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IReminderMailSender _sender;
    private readonly IAuditLogger _audit;
    private readonly ReminderOptions _options;
    private readonly TicketService _tickets;

    public ReminderService(
        IAppDbContext db, IClock clock, ICurrentUser currentUser, IReminderMailSender sender,
        IAuditLogger audit, IOptions<ReminderOptions> options, TicketService tickets)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _sender = sender;
        _audit = audit;
        _options = options.Value;
        _tickets = tickets;
    }

    public async Task<IReadOnlyList<ReminderTemplateDto>> GetTemplatesAsync(CancellationToken ct = default) =>
        await _db.ReminderTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new ReminderTemplateDto(t.Id, t.Code, t.Name, t.SubjectTemplate, t.BodyTemplate, t.IsDefault))
            .ToListAsync(ct);

    public async Task<ReminderPreviewDto> BuildPreviewAsync(ReminderPreviewRequest request, CancellationToken ct = default)
    {
        var recipient = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.RecipientUserId && u.IsActive, ct)
            ?? throw new NotFoundException($"Çalışan bulunamadı: {request.RecipientUserId}");

        if (request.TicketIds.Count == 0)
            throw new DomainRuleException("NO_TICKETS", "En az bir ticket seçilmelidir.");

        var thresholds = await _tickets.GetThresholdsAsync(ct);
        var now = _clock.UtcNow;

        var tickets = await _db.Tickets.AsNoTracking()
            .Include(t => t.AssigneeUser)
            .Include(t => t.MailSources)
            .Where(t => request.TicketIds.Contains(t.Id))
            .ToListAsync(ct);

        if (tickets.Count != request.TicketIds.Count)
            throw new NotFoundException("Seçilen ticket'lardan bazıları bulunamadı.");

        var foreign = tickets.Where(t => t.AssigneeUserId != recipient.Id).ToList();
        if (foreign.Count > 0)
            throw new DomainRuleException("TICKET_NOT_ASSIGNED_TO_RECIPIENT",
                $"Şu ticket'lar seçilen çalışana atanmamış: {string.Join(", ", foreign.Select(t => t.ExternalTicketNumber))}");

        var template = request.TemplateId is { } id
            ? await _db.ReminderTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            : await _db.ReminderTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.IsDefault && t.IsActive, ct);

        var manager = _currentUser.UserId is { } actorId
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct)
            : null;

        var mappedTickets = tickets
            .Select(t => TicketService.Map(t, now, thresholds, false))
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.OriginalSentAtUtc)
            .ToList();

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AssigneeName"] = recipient.DisplayName,
            ["ManagerName"] = manager?.DisplayName ?? "IT Yönetimi",
            ["TicketCount"] = mappedTickets.Count.ToString(CultureInfo.InvariantCulture),
            ["TicketList"] = BuildTicketList(mappedTickets),
            ["Date"] = _clock.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        };

        var subject = Render(template?.SubjectTemplate ?? DefaultSubjectTemplate, values);
        var body = Render(template?.BodyTemplate ?? DefaultBodyTemplate, values);

        var lastSent = await _db.ReminderDeliveries.AsNoTracking()
            .Where(r => r.RecipientUserId == recipient.Id && r.Status == ReminderStatus.Sent)
            .OrderByDescending(r => r.SentAtUtc)
            .Select(r => r.SentAtUtc)
            .FirstOrDefaultAsync(ct);

        return new ReminderPreviewDto(
            recipient.Id, recipient.DisplayName, recipient.Email,
            subject, body, request.Cc ?? [], mappedTickets, lastSent,
            template?.Id, _sender.ProviderName);
    }

    public async Task<ReminderHistoryItemDto> SendAsync(ReminderSendCommand command, CancellationToken ct = default)
    {
        // Müdürün açık onayı olmadan gönderim yapılmaz.
        if (!command.Confirmed)
            throw new DomainRuleException("NOT_CONFIRMED",
                "Hatırlatma maili yalnızca açık onay ile gönderilebilir.");

        if (string.IsNullOrWhiteSpace(command.Subject))
            throw new DomainRuleException("EMPTY_SUBJECT", "Konu boş olamaz.");

        if (string.IsNullOrWhiteSpace(command.Body))
            throw new DomainRuleException("EMPTY_BODY", "Mail gövdesi boş olamaz.");

        if (command.TicketIds.Count == 0)
            throw new DomainRuleException("NO_TICKETS", "En az bir ticket seçilmelidir.");

        var actorId = _currentUser.UserId
                      ?? throw new DomainRuleException("NO_ACTOR", "Oturum açmış kullanıcı bulunamadı.");

        var recipient = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == command.RecipientUserId && u.IsActive, ct)
            ?? throw new NotFoundException($"Çalışan bulunamadı: {command.RecipientUserId}");

        var ticketNumbers = await _db.Tickets.AsNoTracking()
            .Where(t => command.TicketIds.Contains(t.Id))
            .Select(t => t.ExternalTicketNumber)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var cc = command.Cc ?? [];

        var delivery = new ReminderDelivery
        {
            RecipientUserId = recipient.Id,
            SentByUserId = actorId,
            TemplateId = command.TemplateId,
            Subject = command.Subject.Trim(),
            Body = command.Body,
            TicketIdsJson = JsonSerializer.Serialize(command.TicketIds),
            CcJson = cc.Count > 0 ? JsonSerializer.Serialize(cc) : null,
            Status = ReminderStatus.Pending,
            CreatedAtUtc = now
        };

        _db.ReminderDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);

        var result = await _sender.SendAsync(new ReminderSendRequest(
            _options.FromAddress, recipient.Email, cc, delivery.Subject, delivery.Body), ct);

        delivery.Status = result.Success ? ReminderStatus.Sent : ReminderStatus.Failed;
        delivery.ProviderMessageId = result.ProviderMessageId;
        delivery.ErrorMessage = result.Error;
        delivery.SentAtUtc = result.Success ? _clock.UtcNow : null;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("REMINDER_SEND", nameof(ReminderDelivery), delivery.Id.ToString(),
            new { recipient.Email, delivery.Subject, TicketNumbers = ticketNumbers, Result = delivery.Status.ToString() }, ct);

        var actor = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == actorId, ct);

        return new ReminderHistoryItemDto(
            delivery.Id, recipient.Id, recipient.DisplayName, actorId, actor.DisplayName,
            delivery.Subject, ticketNumbers.Count, ticketNumbers, delivery.Status,
            delivery.ErrorMessage, delivery.CreatedAtUtc, delivery.SentAtUtc);
    }

    public async Task<IReadOnlyList<ReminderHistoryItemDto>> GetHistoryAsync(
        ReminderHistoryQuery? query = null, CancellationToken ct = default)
    {
        query ??= new ReminderHistoryQuery();
        var take = Math.Clamp(query.Take, 1, 500);

        var q = _db.ReminderDeliveries.AsNoTracking()
            .Include(r => r.RecipientUser)
            .Include(r => r.SentByUser)
            .AsQueryable();

        if (query.StartDateUtc is { } start)
            q = q.Where(r => r.CreatedAtUtc >= start);

        if (query.EndDateUtc is { } end)
            q = q.Where(r => r.CreatedAtUtc < end.Date.AddDays(1));

        if (query.RecipientUserId is { } recipientId)
            q = q.Where(r => r.RecipientUserId == recipientId);

        if (query.Status is { } status)
            q = q.Where(r => r.Status == status);

        var rows = await q
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var allTicketIds = rows
            .SelectMany(r => DeserializeIds(r.TicketIdsJson))
            .Distinct()
            .ToList();

        var numbers = await _db.Tickets.AsNoTracking()
            .Where(t => allTicketIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.ExternalTicketNumber, ct);

        return rows.Select(r =>
        {
            var ids = DeserializeIds(r.TicketIdsJson);
            var ticketNumbers = ids
                .Select(id => numbers.TryGetValue(id, out var n) ? n : null)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();

            return new ReminderHistoryItemDto(
                r.Id, r.RecipientUserId, r.RecipientUser.DisplayName,
                r.SentByUserId, r.SentByUser.DisplayName,
                r.Subject, ids.Count, ticketNumbers, r.Status, r.ErrorMessage,
                r.CreatedAtUtc, r.SentAtUtc);
        }).ToList();
    }

    private static List<Guid> DeserializeIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildTicketList(IEnumerable<TicketListItemDto> tickets)
    {
        var sb = new StringBuilder();
        foreach (var t in tickets)
        {
            sb.Append("- ")
              .Append(t.ExternalTicketNumber)
              .Append(" | ").Append(t.ApplicationName)
              .Append(" | Öncelik ").Append(t.Priority.ToString(CultureInfo.InvariantCulture))
              .Append(" | ").Append(t.DaysOpen.ToString(CultureInfo.InvariantCulture)).Append(" gündür açık")
              .AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    public const string DefaultSubjectTemplate =
        "Açık ticket hatırlatması — {{TicketCount}} ticket";

    public const string DefaultBodyTemplate =
        """
        Merhaba {{AssigneeName}},

        Üzerinde açık görünen aşağıdaki ticket'lar için güncelleme rica ederim:

        {{TicketList}}

        Durum bilgisi veya tahmini tamamlanma zamanı paylaşabilirsen sevinirim.

        Teşekkürler,
        {{ManagerName}}

        ---
        Bu mail IT Yönetim Paneli üzerinden gönderilmiştir. Panel takip durumu Tixbox durumunu değiştirmez.
        """;
}
