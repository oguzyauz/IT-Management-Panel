using System.Text.RegularExpressions;
using ItCockpit.Domain;
using Microsoft.Extensions.Options;

namespace ItCockpit.Application.Parsing;

/// <summary>
/// Service Desk ticket açılış maillerini deterministik olarak ayrıştırır.
/// Veritabanı, HTTP veya Gmail bağımlılığı yoktur — tek başına test edilebilir.
/// Sözleşme: <c>docs/email-parser-contract.md</c>.
/// </summary>
public sealed partial class TicketMailParser
{
    private readonly TicketMailParserOptions _options;
    private readonly TimeZoneInfo _fallbackZone;

    public TicketMailParser(IOptions<TicketMailParserOptions> options)
        : this(options.Value) { }

    public TicketMailParser(TicketMailParserOptions options)
    {
        _options = options;
        _fallbackZone = MailDateParser.ResolveTimeZone(options.DefaultTimeZone);
    }

    // --- Subject -------------------------------------------------------------------------------

    [GeneratedRegex(@"^\s*(?:(?:Fwd|Fw|FW|RE|Re|İLT|ILT|YNT|VS)\s*:\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForwardPrefixRegex();

    // Uygulama adı ile dış referans arasındaki ayırıcı, boşluklu tire (" - ") olmak zorundadır.
    // Aksi hâlde "SAP BW - REQ-8842 - Priority: 3" gibi konularda dış referanstaki tire,
    // uygulama adının içine kayar ("SAP BW - REQ" gibi hatalı bir değer üretir).
    [GeneratedRegex(
        @"New\s+Ticket\s+n\.\s*(?<ticket>[A-Za-z]\d{6}_\d{6})\s+for\s+(?<requester>.+?)\s+about\s+(?<app>.+?)\s+-\s+(?<extref>.*?)\s*-\s*Priority\s*:\s*(?<priority>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectAnchoredRegex();

    /// <summary>Konu sonunda beklenmeyen metin varsa kullanılan gevşek sürüm.</summary>
    [GeneratedRegex(
        @"New\s+Ticket\s+n\.\s*(?<ticket>[A-Za-z]\d{6}_\d{6})\s+for\s+(?<requester>.+?)\s+about\s+(?<app>.+?)\s+-\s+(?<extref>.*?)\s*-\s*Priority\s*:\s*(?<priority>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectLooseRegex();

    [GeneratedRegex(@"New\s+Ticket\s+n\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectMarkerRegex();

    // --- Gövde ---------------------------------------------------------------------------------

    [GeneratedRegex(@"^[ \t]*Ticket\s+priority[ \t]*:[ \t]*(?<v>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BodyPriorityRegex();

    [GeneratedRegex(@"^[ \t]*Ticket\s+category[ \t]*:[ \t]*(?<v>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BodyCategoryRegex();

    [GeneratedRegex(@"^[ \t]*External\s+Reference[ \t]*:[ \t]*(?<v>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BodyExternalRefRegex();

    [GeneratedRegex(@"ticket\s+number\s+is\s+(?<v>[A-Za-z]\d{6}_\d{6})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BodyTicketNumberRegex();

    [GeneratedRegex(@"@REQUEST_ID@\s*=\s*'?(?<v>[^'\s@]+)'?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequestIdRegex();

    [GeneratedRegex(@"https?://[^\s<>""')\]]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"with\s+the\s+following\s+description\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DescriptionStartRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLinesRegex();

    private static readonly string[] DescriptionStopMarkers =
    [
        "Ticket priority:",
        "Ticket category:",
        "External Reference:",
        "Please take it in charge",
        "Click here",
        "Kindest Regards",
        "Service Desk Menarini",
        "This is an automatic email"
    ];

    private static readonly string[] EmptyExternalReferenceValues = ["N/A", "NA", "-", "", "NONE", "NULL"];

    // -------------------------------------------------------------------------------------------

    public TicketParseResult Parse(RawTicketMail mail)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var body = (mail.Body ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var envelope = ForwardEnvelopeExtractor.TryExtract(body);
        var isForwarded = envelope is not null;

        // --- F1: orijinal gönderen -------------------------------------------------------------
        var originalSenderRaw = envelope?.From ?? mail.From;
        var originalSenderAddress = MailAddressUtils.ExtractAddress(originalSenderRaw);

        if (originalSenderAddress is null ||
            !_options.AllowedSenders.Any(s => string.Equals(s, originalSenderAddress, StringComparison.OrdinalIgnoreCase)))
        {
            return TicketParseResult.Rejected(RejectReasons.SenderNotAllowed);
        }

        // --- F2: konu kalıbı -------------------------------------------------------------------
        var effectiveSubject = StripForwardPrefixes(envelope?.Subject ?? mail.Subject ?? string.Empty);
        if (!SubjectMarkerRegex().IsMatch(effectiveSubject))
            return TicketParseResult.Rejected(RejectReasons.SubjectPatternMismatch);

        var warnings = new List<ParseWarning>();

        var subjectMatch = SubjectAnchoredRegex().Match(effectiveSubject);
        if (!subjectMatch.Success)
        {
            subjectMatch = SubjectLooseRegex().Match(effectiveSubject);
            if (!subjectMatch.Success)
                return TicketParseResult.Rejected(RejectReasons.SubjectPatternMismatch);

            warnings.Add(new ParseWarning(
                ParseWarningCodes.SubjectTrailingContent, ParseWarningSeverity.Info,
                "Konu satırının sonunda beklenmeyen içerik var; gevşek kalıpla ayrıştırıldı.",
                "Subject", effectiveSubject));
        }

        // --- F3: imza --------------------------------------------------------------------------
        if (!body.Contains(_options.RequiredSignature, StringComparison.OrdinalIgnoreCase))
            return TicketParseResult.Rejected(RejectReasons.SignatureMissing);

        // --- F4: ticket numarası formatı -------------------------------------------------------
        var subjectTicketRaw = subjectMatch.Groups["ticket"].Value;
        if (!TicketNumber.TryParse(subjectTicketRaw, out var ticketNumber))
            return TicketParseResult.Rejected(RejectReasons.TicketNumberFormat);

        // --- Gövde alanları --------------------------------------------------------------------
        var innerBody = envelope?.InnerBody ?? body;

        var subjectPriority = int.TryParse(subjectMatch.Groups["priority"].Value, out var sp) ? sp : (int?)null;

        int? bodyPriority = null;
        var bodyPriorityMatch = BodyPriorityRegex().Match(innerBody);
        if (bodyPriorityMatch.Success && int.TryParse(bodyPriorityMatch.Groups["v"].Value, out var bp))
            bodyPriority = bp;

        var priority = bodyPriority ?? subjectPriority ?? 0;
        if (bodyPriority.HasValue && subjectPriority.HasValue && bodyPriority != subjectPriority)
        {
            warnings.Add(new ParseWarning(
                ParseWarningCodes.PriorityMismatch, ParseWarningSeverity.Warning,
                $"Konudaki öncelik ({subjectPriority}) ile gövdedeki öncelik ({bodyPriority}) farklı. Gövdedeki değer kullanıldı.",
                "Priority", subjectPriority.ToString(), bodyPriority.ToString()));
        }

        var categoryMatch = BodyCategoryRegex().Match(innerBody);
        var categoryPath = categoryMatch.Success ? categoryMatch.Groups["v"].Value.Trim() : null;
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            categoryPath = null;
            warnings.Add(new ParseWarning(
                ParseWarningCodes.CategoryMissing, ParseWarningSeverity.Info,
                "Gövdede 'Ticket category:' alanı bulunamadı.", "CategoryPath"));
        }

        var extRefMatch = BodyExternalRefRegex().Match(innerBody);
        var externalReference = NormalizeExternalReference(
            extRefMatch.Success ? extRefMatch.Groups["v"].Value : subjectMatch.Groups["extref"].Value);

        var bodyTicketMatch = BodyTicketNumberRegex().Match(innerBody);
        string? bodyTicketNumber = null;
        if (bodyTicketMatch.Success && TicketNumber.TryParse(bodyTicketMatch.Groups["v"].Value, out var bodyNo))
        {
            bodyTicketNumber = bodyNo.Value;
            if (!string.Equals(bodyNo.Value, ticketNumber.Value, StringComparison.Ordinal))
            {
                warnings.Add(new ParseWarning(
                    ParseWarningCodes.TicketNumberMismatch, ParseWarningSeverity.Error,
                    $"Konudaki ticket numarası ({ticketNumber.Value}) ile gövdedeki numara ({bodyNo.Value}) farklı. Konudaki numara kullanıldı.",
                    "ExternalTicketNumber", ticketNumber.Value, bodyNo.Value));
            }
        }

        var requestIdMatch = RequestIdRegex().Match(innerBody);
        var sourceRequestId = requestIdMatch.Success ? requestIdMatch.Groups["v"].Value.Trim() : null;
        if (sourceRequestId is null)
        {
            warnings.Add(new ParseWarning(
                ParseWarningCodes.RequestIdMissing, ParseWarningSeverity.Info,
                "Gövdede '@REQUEST_ID@' bulunamadı.", "SourceRequestId"));
        }

        var externalUrl = ResolveExternalUrl(innerBody, mail.Links);
        if (externalUrl is null)
        {
            warnings.Add(new ParseWarning(
                ParseWarningCodes.ExternalUrlMissing, ParseWarningSeverity.Info,
                "Tixbox derin bağlantısı bulunamadı.", "ExternalUrl"));
        }

        var description = ExtractDescription(innerBody);
        if (string.IsNullOrWhiteSpace(description))
        {
            warnings.Add(new ParseWarning(
                ParseWarningCodes.DescriptionEmpty, ParseWarningSeverity.Warning,
                "Açıklama bloğu boş.", "Description"));
        }

        // --- Tarih ----------------------------------------------------------------------------
        DateTime originalSentAtUtc;
        if (isForwarded)
        {
            if (MailDateParser.TryParse(envelope!.RawDate, _fallbackZone, out var parsedUtc))
            {
                originalSentAtUtc = parsedUtc;
            }
            else
            {
                originalSentAtUtc = mail.ReceivedAtUtc;
                warnings.Add(new ParseWarning(
                    ParseWarningCodes.OriginalDateUnparsed, ParseWarningSeverity.Warning,
                    $"Forward zarfındaki tarih çözülemedi ('{envelope.RawDate}'). Mailin alınma zamanı kullanıldı.",
                    "OriginalSentAtUtc", null, envelope.RawDate));
            }
        }
        else
        {
            originalSentAtUtc = mail.ReceivedAtUtc;
        }

        var recipients = MailAddressUtils.SplitRecipients(envelope?.To ?? mail.To);

        var ticket = new ParsedTicket
        {
            ExternalTicketNumber = ticketNumber.Value,
            TicketType = ticketNumber.Type,
            RequesterName = PersonNameNormalizer.Normalize(subjectMatch.Groups["requester"].Value),
            ApplicationName = subjectMatch.Groups["app"].Value.Trim(),
            SubjectPriority = subjectPriority,
            Priority = priority,
            CategoryPath = categoryPath,
            ExternalReference = externalReference,
            SourceRequestId = sourceRequestId,
            BodyTicketNumber = bodyTicketNumber,
            OriginalSentAtUtc = originalSentAtUtc,
            OriginalSender = originalSenderAddress,
            OriginalRecipients = recipients,
            OriginalSubject = effectiveSubject,
            Description = description,
            ExternalUrl = externalUrl,
            IsForwarded = isForwarded,
            ForwardedBy = isForwarded ? MailAddressUtils.ExtractAddress(mail.From) : null
        };

        return TicketParseResult.Accepted(ticket, warnings);
    }

    private static string StripForwardPrefixes(string subject) =>
        ForwardPrefixRegex().Replace(subject ?? string.Empty, string.Empty).Trim();

    private static string? NormalizeExternalReference(string? raw)
    {
        var value = raw?.Trim();
        if (value is null) return null;
        return EmptyExternalReferenceValues.Contains(value, StringComparer.OrdinalIgnoreCase) ? null : value;
    }

    private string? ResolveExternalUrl(string body, IReadOnlyList<string>? links)
    {
        var hint = _options.ExternalUrlHostHint;

        var fromLinks = links?.FirstOrDefault(l => l.Contains(hint, StringComparison.OrdinalIgnoreCase));
        if (fromLinks is not null) return fromLinks;

        var fromBody = UrlRegex().Matches(body)
            .Select(m => m.Value)
            .FirstOrDefault(u => u.Contains(hint, StringComparison.OrdinalIgnoreCase));

        return fromBody;
    }

    private static string ExtractDescription(string body)
    {
        var start = 0;
        var startMatch = DescriptionStartRegex().Match(body);
        if (startMatch.Success)
        {
            start = startMatch.Index + startMatch.Length;
        }

        var region = body[start..];

        var stop = region.Length;
        foreach (var marker in DescriptionStopMarkers)
        {
            var idx = region.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < stop) stop = idx;
        }

        var text = region[..stop];
        text = ExcessBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim('\n', ' ', '\t');
    }
}
