namespace ItCockpit.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tixbox ticket numarası, kanonik format <c>^[IS]\d{6}_\d{6}$</c>. Duplicate anahtarı #2.</summary>
    public string ExternalTicketNumber { get; set; } = null!;

    public TicketType TicketType { get; set; }

    /// <summary>Normalize edilmiş talep eden adı ("Turcan, Merve" -&gt; "Merve Turcan").</summary>
    public string RequesterName { get; set; } = null!;

    public string ApplicationName { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? CategoryPath { get; set; }
    public string? ExternalReference { get; set; }

    /// <summary>Mail gövdesindeki <c>@REQUEST_ID@</c> değeri. Duplicate anahtarı #3.</summary>
    public string? SourceRequestId { get; set; }

    /// <summary>Ticket'ın Tixbox'ta açıldığı an. Forward edilmiş mailde <b>iç</b> zarfın tarihidir.</summary>
    public DateTime OriginalSentAtUtc { get; set; }

    /// <summary>Tixbox derin bağlantısı. Yalnızca yeni sekmede açılır; hiçbir yazma işlemi yapılmaz.</summary>
    public string? ExternalUrl { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Unassigned;
    public Guid? AssigneeUserId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }

    /// <summary>
    /// Ticket maili tek bir kişiye gönderildiği için sistem tarafından otomatik atandı.
    /// Müdürün "ben atamadım, kendi geldi" ayrımını yapabilmesi için tutulur.
    /// </summary>
    public bool AutoAssigned { get; set; }

    /// <summary>
    /// Mailden değil, panelden elle girildi. Mail kaynağı olmadığı için ayrı işaretlenir —
    /// aksi hâlde "bu ticket nereden geldi" sorusu cevapsız kalır.
    /// </summary>
    public bool CreatedManually { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    public User? AssigneeUser { get; set; }
    public ICollection<TicketAssignment> Assignments { get; set; } = new List<TicketAssignment>();
    public ICollection<TicketStatusHistory> StatusHistory { get; set; } = new List<TicketStatusHistory>();
    public ICollection<TicketNote> Notes { get; set; } = new List<TicketNote>();
    public ICollection<TicketMailSource> MailSources { get; set; } = new List<TicketMailSource>();
    public ICollection<TicketParseWarning> ParseWarnings { get; set; } = new List<TicketParseWarning>();
}

public class TicketAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid AssignedToUserId { get; set; }

    /// <summary>Sistem tarafından yapılan otomatik atamalarda <c>null</c>.</summary>
    public Guid? AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
    public DateTime? UnassignedAtUtc { get; set; }
    public string? Note { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User AssignedToUser { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}

public class TicketStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public TicketStatus? FromStatus { get; set; }
    public TicketStatus ToStatus { get; set; }

    /// <summary>Sistem tarafından yapılan geçişlerde (mail ile oluşturma) <c>null</c>.</summary>
    public Guid? ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; }
    public string? Note { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}

public class TicketNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}

/// <summary>Bir ticket'a ait her mail (ilk düşen + sonradan gelen forward'lar) burada izlenir.</summary>
public class TicketMailSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }

    /// <summary>
    /// Bu mailin okunduğu posta kutusu. Aynı ticket birden fazla kutuda bulunabildiği için
    /// (mail bir gruba gidiyor) hangi kutudan geldiğinin izlenmesi gerekir.
    /// </summary>
    public string SourceMailbox { get; set; } = string.Empty;

    /// <summary>Duplicate anahtarı #1. Aynı mail farklı kutularda <b>farklı</b> kimlikle görünür.</summary>
    public string GmailMessageId { get; set; } = null!;

    public string GmailThreadId { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string OriginalSender { get; set; } = null!;

    /// <summary>Orijinal alıcılar, JSON dizi.</summary>
    public string OriginalRecipients { get; set; } = "[]";

    /// <summary>Forward eden kişi (dış zarf göndereni). Doğrudan mailde null.</summary>
    public string? ForwardedBy { get; set; }

    public bool IsForwarded { get; set; }
    public DateTime OriginalSentAtUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime IngestedAtUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}

public class TicketParseWarning
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Reddedilen mailler için null olabilir.</summary>
    public Guid? TicketId { get; set; }

    public string GmailMessageId { get; set; } = null!;
    public string Code { get; set; } = null!;
    public ParseWarningSeverity Severity { get; set; }
    public string Message { get; set; } = null!;
    public string? FieldName { get; set; }
    public string? SubjectValue { get; set; }
    public string? BodyValue { get; set; }
    public bool IsAcknowledged { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Ticket? Ticket { get; set; }
}
