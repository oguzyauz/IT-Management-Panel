using ItCockpit.Domain;

namespace ItCockpit.Application.Contracts;

public sealed record TicketListItemDto(
    Guid Id,
    string ExternalTicketNumber,
    TicketType TicketType,
    string RequesterName,
    string ApplicationName,
    int Priority,
    TicketStatus Status,
    Guid? AssigneeUserId,
    string? AssigneeName,
    DateTime OriginalSentAtUtc,
    DateTime UpdatedAtUtc,
    int DaysOpen,
    int DaysSinceUpdate,
    AgingLevel Aging,
    bool HasParseWarning,
    // Kişiye özel mail olduğu için sistem tarafından atandı; müdür ataması değil.
    bool AutoAssigned,
    // Mailden değil, panelden elle girildi.
    bool CreatedManually,
    // Ticket'ın maili hangi posta kutusunda okundu. Aynı mail birden fazla kutuya
    // düşebildiği için liste; elle eklenen kayıtlarda boştur.
    IReadOnlyList<string> SourceMailboxes,
    string? ExternalUrl);

public enum AgingLevel
{
    Normal = 0,
    /// <summary>Belirlenen gün sayısıdır güncellenmemiş.</summary>
    NeedsUpdate = 1,
    /// <summary>Uzun süredir açık.</summary>
    LongOpen = 2,
    /// <summary>Uzun süredir açık — kritik eşik.</summary>
    LongOpenCritical = 3
}

public sealed record TicketDetailDto(
    Guid Id,
    string ExternalTicketNumber,
    TicketType TicketType,
    string RequesterName,
    string ApplicationName,
    string Description,
    int Priority,
    string? CategoryPath,
    string? ExternalReference,
    string? SourceRequestId,
    DateTime OriginalSentAtUtc,
    string? ExternalUrl,
    TicketStatus Status,
    IReadOnlyList<TicketStatus> AllowedNextStatuses,
    Guid? AssigneeUserId,
    string? AssigneeName,
    bool AutoAssigned,
    bool CreatedManually,
    DateTime? AssignedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int DaysOpen,
    int DaysSinceUpdate,
    AgingLevel Aging,
    IReadOnlyList<TicketNoteDto> Notes,
    IReadOnlyList<TicketStatusHistoryDto> StatusHistory,
    IReadOnlyList<TicketAssignmentDto> Assignments,
    IReadOnlyList<TicketMailSourceDto> MailSources,
    IReadOnlyList<ParseWarningDto> ParseWarnings);

public sealed record TicketNoteDto(Guid Id, Guid AuthorUserId, string AuthorName, string Body, DateTime CreatedAtUtc);

public sealed record TicketStatusHistoryDto(
    Guid Id, TicketStatus? FromStatus, TicketStatus ToStatus,
    Guid? ChangedByUserId, string ChangedByName, DateTime ChangedAtUtc, string? Note);

public sealed record TicketAssignmentDto(
    Guid Id, Guid AssignedToUserId, string AssignedToName,
    Guid? AssignedByUserId, string AssignedByName,
    DateTime AssignedAtUtc, DateTime? UnassignedAtUtc, string? Note);

public sealed record TicketMailSourceDto(
    Guid Id, string SourceMailbox, string GmailMessageId, string Subject, string OriginalSender,
    IReadOnlyList<string> OriginalRecipients, string? ForwardedBy, bool IsForwarded,
    DateTime OriginalSentAtUtc, DateTime ReceivedAtUtc, DateTime IngestedAtUtc);

public sealed record ParseWarningDto(
    Guid Id, Guid? TicketId, string? TicketNumber, string GmailMessageId, string Code,
    ParseWarningSeverity Severity, string Message, string? FieldName,
    string? SubjectValue, string? BodyValue, bool IsAcknowledged, DateTime CreatedAtUtc);

public sealed record TicketQuery(
    string? Search = null,
    TicketStatus[]? Statuses = null,
    Guid? AssigneeUserId = null,
    bool? Unassigned = null,
    int? Priority = null,
    string? ApplicationName = null,
    AgingLevel? MinAging = null,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "OriginalSentAtUtc",
    bool SortDescending = true);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Panelden elle ticket açma. Tixbox'ta var olan ama maili panele düşmemiş kayıtlar için.
/// Tixbox'a hiçbir şey yazılmaz — yalnızca takip kaydı oluşturulur.
/// </summary>
public sealed record CreateTicketRequest(
    string ExternalTicketNumber,
    string RequesterName,
    string ApplicationName,
    int Priority,
    DateTime OriginalSentAtUtc,
    string? Description,
    string? CategoryPath,
    string? ExternalReference,
    string? ExternalUrl,
    Guid? AssigneeUserId);

public sealed record AssignTicketRequest(Guid AssigneeUserId, string? Note);
public sealed record ChangeStatusRequest(TicketStatus Status, string? Note);
public sealed record AddNoteRequest(string Body);
