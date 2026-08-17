using ItCockpit.Domain;

namespace ItCockpit.Application.Contracts;

public sealed record ReminderPreviewRequest(
    Guid RecipientUserId,
    IReadOnlyList<Guid> TicketIds,
    Guid? TemplateId,
    IReadOnlyList<string>? Cc);

public sealed record ReminderPreviewDto(
    Guid RecipientUserId,
    string RecipientName,
    string RecipientEmail,
    string Subject,
    string Body,
    IReadOnlyList<string> Cc,
    IReadOnlyList<TicketListItemDto> Tickets,
    DateTime? LastReminderSentAtUtc,
    Guid? TemplateId,
    string ProviderName);

/// <summary>
/// Gönderim isteği. Konu ve gövde müdür tarafından düzenlenmiş hâliyle gelir —
/// sunucu preview'ı yeniden üretmez, açık onaylanan metni gönderir.
/// </summary>
public sealed record ReminderSendCommand(
    Guid RecipientUserId,
    IReadOnlyList<Guid> TicketIds,
    Guid? TemplateId,
    string Subject,
    string Body,
    IReadOnlyList<string>? Cc,
    bool Confirmed);

public sealed record ReminderHistoryItemDto(
    Guid Id,
    Guid RecipientUserId,
    string RecipientName,
    Guid SentByUserId,
    string SentByName,
    string Subject,
    int TicketCount,
    IReadOnlyList<string> TicketNumbers,
    ReminderStatus Status,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc);

public sealed record ReminderTemplateDto(
    Guid Id, string Code, string Name, string SubjectTemplate, string BodyTemplate, bool IsDefault);
