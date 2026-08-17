using System.Text.RegularExpressions;

namespace ItCockpit.Application.Parsing;

public static partial class MailAddressUtils
{
    [GeneratedRegex(@"<\s*(?<addr>[^<>@\s]+@[^<>@\s]+)\s*>", RegexOptions.CultureInvariant)]
    private static partial Regex AngleAddressRegex();

    [GeneratedRegex(@"[^<>@\s,;]+@[^<>@\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex BareAddressRegex();

    /// <summary>
    /// <c>"Keydal, Duygu &lt;dkeydal@menarini.com.tr&gt;"</c> gibi bir değerden yalnızca adresi çıkarır.
    /// Açılı parantez yoksa metindeki ilk adres kalıbı döner.
    /// </summary>
    public static string? ExtractAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var m = AngleAddressRegex().Match(value);
        if (m.Success) return m.Groups["addr"].Value.Trim();

        var bare = BareAddressRegex().Match(value);
        return bare.Success ? bare.Value.Trim() : null;
    }

    /// <summary>
    /// Alıcı listesini ayrıştırır. "Soyad, Ad &lt;adres&gt;" biçiminde virgül hem ad ayırıcısı hem
    /// liste ayırıcısı olduğu için basit <c>Split(',')</c> kullanılamaz — adres sınırları esas alınır.
    /// </summary>
    public static IReadOnlyList<string> SplitRecipients(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        var withAngles = AngleAddressRegex().Matches(value)
            .Select(m => m.Groups["addr"].Value.Trim())
            .ToList();

        if (withAngles.Count > 0) return withAngles;

        return BareAddressRegex().Matches(value)
            .Select(m => m.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool AddressEquals(string? a, string? b) =>
        string.Equals(ExtractAddress(a), ExtractAddress(b), StringComparison.OrdinalIgnoreCase);
}
