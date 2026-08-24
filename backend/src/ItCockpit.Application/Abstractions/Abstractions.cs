using ItCockpit.Application.Parsing;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
    TimeZoneInfo LocalZone { get; }
    DateOnly Today { get; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
}

/// <summary>AppSettings tablosundan okunan, çalışma zamanında değiştirilebilir ayarlar.</summary>
public interface IAppSettingsProvider
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task SetAsync(string key, string value, Guid? updatedByUserId, CancellationToken ct = default);
    void Invalidate();
}

/// <summary>
/// Gmail'den filtreli ticket maili okuma soyutlaması.
/// <see cref="Infrastructure"/> katmanında iki implementasyonu vardır: gerçek Gmail ve mock.
/// </summary>
public interface IGmailTicketSource
{
    string ProviderName { get; }

    /// <summary>Yalnızca ticket etiketine, izinli gönderene ve konu kalıbına uyan mailleri döner.</summary>
    Task<IReadOnlyList<RawTicketMail>> FetchAsync(GmailFetchRequest request, CancellationToken ct = default);
}

/// <summary>
/// OAuth onayını mail okumadan <b>ayrı</b> tetiklemek için. İlk yetkilendirmede tarayıcı açılır ve
/// kullanıcı onay verene kadar beklenir; bu, ingestion isteğinin zaman aşımına uğramasını önler.
/// Yalnızca gerçek Gmail sağlayıcısı tarafından uygulanır.
/// </summary>
public interface IGmailAuthorizer
{
    /// <summary>
    /// Belirtilen posta kutusu için yetkilendirmeyi tamamlar ve gerçekte yetki verilen hesabın
    /// adresini döner. Her kutu ayrı ayrı onaylanır; token kutu adresine göre saklanır.
    /// </summary>
    Task<string> AuthorizeAsync(string mailbox, CancellationToken ct = default);
}

public sealed record GmailFetchRequest(
    string MailboxAddress,
    string? LabelName,
    IReadOnlyList<string> AllowedSenders,
    string SubjectContains,
    string? SinceHistoryId,
    DateTime? SinceUtc,
    int MaxResults = 100);

public interface IReminderMailSender
{
    string ProviderName { get; }
    Task<ReminderSendResult> SendAsync(ReminderSendRequest request, CancellationToken ct = default);
}

public sealed record ReminderSendRequest(
    string FromAddress,
    string ToAddress,
    IReadOnlyList<string> CcAddresses,
    string Subject,
    string Body);

public sealed record ReminderSendResult(bool Success, string? ProviderMessageId, string? Error);

public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default);
}

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<Team> Teams { get; }

    DbSet<Ticket> Tickets { get; }
    DbSet<TicketAssignment> TicketAssignments { get; }
    DbSet<TicketStatusHistory> TicketStatusHistory { get; }
    DbSet<TicketNote> TicketNotes { get; }
    DbSet<TicketMailSource> TicketMailSources { get; }
    DbSet<TicketParseWarning> TicketParseWarnings { get; }

    DbSet<WorkScheduleWeek> WorkScheduleWeeks { get; }
    DbSet<WorkScheduleDay> WorkScheduleDays { get; }
    DbSet<WorkScheduleApproval> WorkScheduleApprovals { get; }
    DbSet<WorkCalendarEntry> WorkCalendar { get; }

    DbSet<ReminderTemplate> ReminderTemplates { get; }
    DbSet<ReminderDelivery> ReminderDeliveries { get; }

    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<GmailSyncState> GmailSyncStates { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
