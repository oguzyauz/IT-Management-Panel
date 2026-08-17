using System.Globalization;
using System.Text.RegularExpressions;

namespace ItCockpit.Application.Parsing;

/// <summary>
/// Forward zarfındaki <c>Date:</c> satırını çözer. Gmail'in ürettiği
/// <c>"Wed, 29 Jul 2026 at 11:47"</c> biçimi offset içermez; bu durumda değer, yapılandırılan
/// yerel saat dilimi (varsayılan Europe/Istanbul) kabul edilip UTC'ye çevrilir.
/// </summary>
public static partial class MailDateParser
{
    private static readonly string[] Formats =
    [
        "ddd, d MMM yyyy 'at' HH:mm",
        "ddd, d MMM yyyy 'at' H:mm",
        "ddd, d MMM yyyy HH:mm:ss zzz",
        "ddd, d MMM yyyy HH:mm zzz",
        "ddd, d MMM yyyy HH:mm:ss",
        "ddd, d MMM yyyy HH:mm",
        "d MMM yyyy 'at' HH:mm",
        "d MMM yyyy HH:mm:ss zzz",
        "d MMM yyyy HH:mm"
    ];

    [GeneratedRegex(@"(?<sign>[+-])(?<h>\d{2})(?<m>\d{2})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex CompactOffsetRegex();

    [GeneratedRegex(@"\s*\((?<tz>[A-Za-z]{2,5})\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingTimeZoneNameRegex();

    [GeneratedRegex(@"(?:[+-]\d{2}:?\d{2}|\bGMT\b|\bUTC\b|Z)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffsetRegex();

    public static bool TryParse(string? raw, TimeZoneInfo fallbackZone, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var value = raw.Trim();

        // "... 11:47:00 +0300 (GMT+03:00)" -> sondaki isim kaldırılır, offset iki noktalı hâle gelir.
        value = TrailingTimeZoneNameRegex().Replace(value, string.Empty).Trim();
        value = CompactOffsetRegex().Replace(value, m => $"{m.Groups["sign"].Value}{m.Groups["h"].Value}:{m.Groups["m"].Value}");

        const DateTimeStyles offsetStyles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal;

        foreach (var format in Formats)
        {
            var hasOffset = format.Contains("zzz", StringComparison.Ordinal);

            if (hasOffset)
            {
                if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, offsetStyles, out var withOffset))
                {
                    utc = DateTime.SpecifyKind(withOffset, DateTimeKind.Utc);
                    return true;
                }
            }
            else if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                         DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var local))
            {
                utc = ToUtc(local, fallbackZone);
                return true;
            }
        }

        // Son çare 1: metinde açık bir offset varsa DateTimeOffset ile çöz.
        if (ExplicitOffsetRegex().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        // Son çare 2: offset yok — değer yerel saat kabul edilir.
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var loose))
        {
            utc = ToUtc(DateTime.SpecifyKind(loose, DateTimeKind.Unspecified), fallbackZone);
            return true;
        }

        return false;
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }

    /// <summary>IANA kimliğini dener; Windows'ta bulunamazsa Windows kimliğine düşer.</summary>
    public static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return id switch
            {
                "Europe/Istanbul" => TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"),
                _ => TimeZoneInfo.Utc
            };
        }
    }
}
