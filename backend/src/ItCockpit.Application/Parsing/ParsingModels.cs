using ItCockpit.Domain;

namespace ItCockpit.Application.Parsing;

/// <summary>Gmail'den gelen ham mail. Parser'ın tek girdisidir.</summary>
public sealed record RawTicketMail(
    string GmailMessageId,
    string GmailThreadId,
    string Subject,
    string From,
    string To,
    DateTime ReceivedAtUtc,
    string Body,
    IReadOnlyList<string> Links)
{
    public static RawTicketMail Empty { get; } =
        new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, default, string.Empty, []);
}

public sealed record ParsedTicket
{
    public required string ExternalTicketNumber { get; init; }
    public required TicketType TicketType { get; init; }
    public required string RequesterName { get; init; }
    public required string ApplicationName { get; init; }
    public int? SubjectPriority { get; init; }
    public required int Priority { get; init; }
    public string? CategoryPath { get; init; }
    public string? ExternalReference { get; init; }
    public string? SourceRequestId { get; init; }
    public string? BodyTicketNumber { get; init; }
    public required DateTime OriginalSentAtUtc { get; init; }
    public required string OriginalSender { get; init; }
    public required IReadOnlyList<string> OriginalRecipients { get; init; }
    public required string OriginalSubject { get; init; }
    public required string Description { get; init; }
    public string? ExternalUrl { get; init; }
    public required bool IsForwarded { get; init; }
    public string? ForwardedBy { get; init; }
}

public sealed record ParseWarning(
    string Code,
    ParseWarningSeverity Severity,
    string Message,
    string? FieldName = null,
    string? SubjectValue = null,
    string? BodyValue = null);

public sealed record TicketParseResult(
    bool IsTicketMail,
    ParsedTicket? Ticket,
    IReadOnlyList<ParseWarning> Warnings,
    string? RejectReason)
{
    public static TicketParseResult Rejected(string reason) => new(false, null, [], reason);
    public static TicketParseResult Accepted(ParsedTicket ticket, IReadOnlyList<ParseWarning> warnings) =>
        new(true, ticket, warnings, null);
}

public static class ParseWarningCodes
{
    public const string TicketNumberMismatch = "TICKET_NUMBER_MISMATCH";
    public const string PriorityMismatch = "PRIORITY_MISMATCH";
    public const string OriginalDateUnparsed = "ORIGINAL_DATE_UNPARSED";
    public const string SubjectTrailingContent = "SUBJECT_TRAILING_CONTENT";
    public const string ExternalUrlMissing = "EXTERNAL_URL_MISSING";
    public const string CategoryMissing = "CATEGORY_MISSING";
    public const string DescriptionEmpty = "DESCRIPTION_EMPTY";
    public const string RequestIdMissing = "REQUEST_ID_MISSING";
}

public static class RejectReasons
{
    public const string SenderNotAllowed = "SENDER_NOT_ALLOWED";
    public const string SubjectPatternMismatch = "SUBJECT_PATTERN_MISMATCH";
    public const string SignatureMissing = "SIGNATURE_MISSING";
    public const string TicketNumberFormat = "TICKET_NUMBER_FORMAT";
}
