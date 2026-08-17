using System.Globalization;
using System.Text.RegularExpressions;

namespace ItCockpit.Domain;

/// <summary>
/// Service Desk maillerindeki <c>"Soyad, Ad"</c> biçimini <c>"Ad Soyad"</c> hâline getirir.
/// Türkçe karakterler nedeniyle tüm büyük/küçük harf dönüşümleri tr-TR kültürüyle yapılır
/// (aksi hâlde "IŞIK" -&gt; "Isik" gibi bozulmalar oluşur).
/// </summary>
public static partial class PersonNameNormalizer
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var collapsed = WhitespaceRegex().Replace(raw.Trim(), " ");

        var commaCount = collapsed.Count(c => c == ',');
        if (commaCount == 1)
        {
            var parts = collapsed.Split(',', 2);
            var surname = parts[0].Trim();
            var given = parts[1].Trim();

            // Her iki parça da doluysa sırayı çevir; biri boşsa dokunma.
            if (surname.Length > 0 && given.Length > 0)
                collapsed = $"{given} {surname}";
        }

        return ToTitleCase(collapsed);
    }

    private static string ToTitleCase(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            words[i] = w.Length == 1
                ? w.ToUpper(Turkish)
                : string.Concat(w[..1].ToUpper(Turkish), w[1..].ToLower(Turkish));
        }

        return string.Join(' ', words);
    }
}
