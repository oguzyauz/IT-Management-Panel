using System.Text.RegularExpressions;

namespace ItCockpit.Application.Parsing;

public sealed record ForwardEnvelope(
    string? From,
    string? RawDate,
    string? Subject,
    string? To,
    string? Cc,
    string InnerBody,
    int SeparatorIndex);

/// <summary>
/// Forward edilmiş mailin <b>en içteki</b> orijinal zarfını bulur.
/// Zincirli forward'larda (A -&gt; B -&gt; C) en son ayırıcı orijinal maile aittir; bu yüzden
/// ilk değil <b>son</b> eşleşme kullanılır.
/// </summary>
public static partial class ForwardEnvelopeExtractor
{
    [GeneratedRegex(@"^[ \t]*-{2,}\s*Forwarded message\s*-{2,}[ \t]*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GmailSeparatorRegex();

    [GeneratedRegex(@"^[ \t]*-{2,}\s*Original Message\s*-{2,}[ \t]*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex OutlookSeparatorRegex();

    [GeneratedRegex(@"^[ \t]*Begin forwarded message:[ \t]*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AppleSeparatorRegex();

    /// <summary>Outlook Web: uzun alt çizgi bloğunu hemen izleyen <c>From:</c> satırı.</summary>
    [GeneratedRegex(@"^[ \t]*_{10,}[ \t]*$(?=(?:\r?\n)+[ \t]*From[ \t]*:)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex OutlookWebSeparatorRegex();

    [GeneratedRegex(@"^[ \t]*(?<key>From|Date|Sent|Subject|To|Cc)[ \t]*:[ \t]*(?<value>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderLineRegex();

    public static ForwardEnvelope? TryExtract(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        var separatorEnd = FindLastSeparatorEnd(body);
        if (separatorEnd < 0) return null;

        var rest = body[separatorEnd..].TrimStart('\r', '\n');
        var lines = rest.Split('\n');

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? lastKey = null;
        var consumed = 0;
        var seenHeader = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
            {
                // Boş satır başlık bloğunu tek başına bitirmez: HTML'den düzleştirilen gövdelerde
                // başlıklar arasına boş satır girebiliyor. Blok, ancak sonraki dolu satır da
                // başlık değilse biter.
                var next = NextNonEmpty(lines, i + 1);
                if (seenHeader && (next is null || !HeaderLineRegex().IsMatch(next)))
                {
                    consumed = i + 1;
                    break;
                }

                lastKey = null;   // katlama zinciri boş satırda kesilir (gövde metni yutulmasın)
                consumed = i + 1;
                continue;
            }

            var m = HeaderLineRegex().Match(line);
            if (m.Success)
            {
                seenHeader = true;
                lastKey = m.Groups["key"].Value.ToLowerInvariant();
                if (!headers.ContainsKey(lastKey))
                    headers[lastKey] = m.Groups["value"].Value.Trim();

                consumed = i + 1;
                continue;
            }

            // Katlanmış başlık devamı. Gmail'in text/plain sürümü uzun satırları ~78 karakterde
            // böler; "Subject: ... - N/A -" / "Priority: 2" gibi ikiye ayrılan konular birleştirilir.
            if (seenHeader && lastKey is not null)
            {
                headers[lastKey] = $"{headers[lastKey]} {line.Trim()}".Trim();
                consumed = i + 1;
                continue;
            }

            break;   // ayırıcıdan hemen sonra başlık yoksa forward sayılmaz
        }

        if (!seenHeader) return null;

        var innerBody = string.Join('\n', lines.Skip(consumed)).Trim('\r', '\n');

        return new ForwardEnvelope(
            headers.GetValueOrDefault("from"),
            headers.GetValueOrDefault("date") ?? headers.GetValueOrDefault("sent"),
            headers.GetValueOrDefault("subject"),
            headers.GetValueOrDefault("to"),
            headers.GetValueOrDefault("cc"),
            innerBody,
            separatorEnd);
    }

    private static string? NextNonEmpty(string[] lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(line)) return line;
        }

        return null;
    }

    private static int FindLastSeparatorEnd(string body)
    {
        var best = -1;

        foreach (var regex in new[]
                 {
                     GmailSeparatorRegex(), OutlookSeparatorRegex(),
                     AppleSeparatorRegex(), OutlookWebSeparatorRegex()
                 })
        {
            foreach (Match m in regex.Matches(body))
            {
                var end = m.Index + m.Length;
                if (end > best) best = end;
            }
        }

        return best;
    }
}
