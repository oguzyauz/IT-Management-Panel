using System.Text.RegularExpressions;

namespace ItCockpit.Domain;

/// <summary>
/// Tixbox ticket numarasının kanonik gösterimi.
/// Format prompt tarafından <c>^[IS]\d{6}_\d{6}$</c> olarak sabitlenmiştir; eski SRS'in daha esnek
/// <c>[A-Z]{1,2}</c> önerisi bilinçli olarak uygulanmamıştır (bkz. docs/open-questions.md A1).
/// </summary>
public readonly partial record struct TicketNumber
{
    public string Value { get; }
    public TicketType Type { get; }

    private TicketNumber(string value, TicketType type)
    {
        Value = value;
        Type = type;
    }

    [GeneratedRegex(@"^[ISis]\d{6}_\d{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRegex();

    public static bool TryParse(string? raw, out TicketNumber number)
    {
        number = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim();
        if (!CanonicalRegex().IsMatch(trimmed)) return false;

        // Numaranın harf kısmı daima ASCII'ye çevrilir; Türkçe kültüründe 'I'.ToUpper() sorunundan
        // kaçınmak için invariant kullanılır.
        var normalized = char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        var type = normalized[0] == 'I' ? TicketType.Incident : TicketType.ServiceRequest;

        number = new TicketNumber(normalized, type);
        return true;
    }

    public override string ToString() => Value;
}
