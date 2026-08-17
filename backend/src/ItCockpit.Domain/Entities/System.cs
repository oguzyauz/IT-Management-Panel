namespace ItCockpit.Domain.Entities;

public class ReminderTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string SubjectTemplate { get; set; } = null!;
    public string BodyTemplate { get; set; } = null!;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Gönderim kanıtı. Silinmez.</summary>
public class ReminderDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientUserId { get; set; }
    public Guid SentByUserId { get; set; }
    public Guid? TemplateId { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string TicketIdsJson { get; set; } = "[]";
    public string? CcJson { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public User RecipientUser { get; set; } = null!;
    public User SentByUser { get; set; } = null!;
    public ReminderTemplate? Template { get; set; }
}

/// <summary>Denetim kaydı. Hiçbir koşulda silinmez, soft-delete de uygulanmaz.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? ChangesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}

public class AppSetting
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string DataType { get; set; } = "string";
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public class GmailSyncState
{
    public int Id { get; set; }
    public string MailboxAddress { get; set; } = null!;
    public string? LastHistoryId { get; set; }
    public DateTime? LastSyncStartedAtUtc { get; set; }
    public DateTime? LastSyncCompletedAtUtc { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastError { get; set; }
    public int MessagesSeen { get; set; }
    public int TicketsCreated { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int MailsRejected { get; set; }
}

public static class AppSettingKeys
{
    public const string AgingStaleAfterDays = "Aging.StaleAfterDays";
    public const string AgingOldAfterDays = "Aging.OldAfterDays";
    public const string AgingCriticalAfterDays = "Aging.CriticalAfterDays";
    public const string ScheduleRequiredOfficeDays = "Schedule.RequiredOfficeDays";
    public const string ScheduleRequiredHomeOfficeDays = "Schedule.RequiredHomeOfficeDays";
    public const string ScheduleLockDayOfWeek = "Schedule.LockDayOfWeek";
    public const string ScheduleLockTimeLocal = "Schedule.LockTimeLocal";
    public const string GmailPollIntervalMinutes = "Gmail.PollIntervalMinutes";

    /// <summary>
    /// Okunacak posta kutuları, satır başıyla ayrılmış. Arayüzden düzenlenebilmesi için
    /// appsettings.json yerine burada tutulur; boşsa yapılandırmadaki listeye düşülür.
    /// </summary>
    public const string GmailMailboxes = "Gmail.Mailboxes";
}
