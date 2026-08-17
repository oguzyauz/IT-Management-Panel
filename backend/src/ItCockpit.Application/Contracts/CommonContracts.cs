namespace ItCockpit.Application.Contracts;

public sealed record UserDto(
    Guid Id, string Email, string DisplayName, string? Title,
    Guid? TeamId, string? TeamName, IReadOnlyList<string> Roles);

public sealed record IngestionRunResultDto(
    string Provider,
    int MessagesSeen,
    int TicketsCreated,
    int DuplicatesSkipped,
    int MailsRejected,
    int WarningsRaised,
    IReadOnlyList<string> CreatedTicketNumbers,
    IReadOnlyList<string> RejectReasons,
    IReadOnlyList<MailboxIngestionResultDto> PerMailbox,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc);

/// <summary>Tek bir posta kutusunun okuma sonucu. Hata alan kutu diğerlerini durdurmaz.</summary>
public sealed record MailboxIngestionResultDto(
    string MailboxAddress,
    int MessagesSeen,
    int TicketsCreated,
    int DuplicatesSkipped,
    int MailsRejected,
    string? Error);

/// <summary>
/// Kuru çalıştırma sonucu — veritabanına <b>hiçbir şey yazılmaz</b>. Gerçek bir kutuya
/// bağlanırken parser'ın ne gördüğünü ve neden kabul/red ettiğini teşhis etmek içindir.
/// Mail gövdesi döndürülmez; yalnızca yapısal alanlar raporlanır.
/// </summary>
public sealed record IngestionPreviewDto(
    string Provider,
    int MessagesSeen,
    IReadOnlyList<IngestionPreviewItemDto> Items);

public sealed record IngestionPreviewItemDto(
    string SourceMailbox,
    string GmailMessageId,
    string OuterSubject,
    string OuterFrom,
    DateTime ReceivedAtUtc,
    int BodyLength,
    bool ForwardEnvelopeFound,
    string? EnvelopeFromLine,
    string? EnvelopeDateLine,
    string? EnvelopeSubjectLine,
    bool IsTicketMail,
    string? RejectReason,
    string? ExternalTicketNumber,
    string? RequesterName,
    string? ApplicationName,
    int? Priority,
    DateTime? OriginalSentAtUtc,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Kutuya hiç erişilemediğinde (ör. yetkilendirilmemiş) tek satırlık hata kaydı.</summary>
    public static IngestionPreviewItemDto ForMailboxError(string mailbox, string error) =>
        new(mailbox, string.Empty, string.Empty, string.Empty, default, 0,
            false, null, null, null, false, $"MAILBOX_ERROR: {error}",
            null, null, null, null, null, []);
}

public sealed record GmailSyncStateDto(
    string MailboxAddress, string? LastHistoryId, DateTime? LastSyncCompletedAtUtc,
    string? LastSyncStatus, string? LastError,
    int MessagesSeen, int TicketsCreated, int DuplicatesSkipped, int MailsRejected);

public sealed record AppSettingDto(string Key, string Value, string DataType, string Category, string? Description);

public sealed record UpdateAppSettingsRequest(IReadOnlyDictionary<string, string> Values);

/// <summary>İş kuralı ihlallerinde fırlatılır; API katmanında 400/409'a çevrilir.</summary>
public sealed class DomainRuleException : Exception
{
    public string Code { get; }

    public DomainRuleException(string code, string message) : base(message) => Code = code;
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
