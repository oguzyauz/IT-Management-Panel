using System.Security.Cryptography;

namespace ItCockpit.Application.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256 ile parola özetleme.
///
/// Dışarıdan bir kimlik kütüphanesi eklemek yerine .NET'in kendi
/// <see cref="Rfc2898DeriveBytes"/> uygulaması kullanılır: bağımlılık yok, algoritma
/// bilinen ve denetlenebilir. Özet, doğrulama sırasında parametrelerin okunabilmesi için
/// kendi kendini tanımlayan bir metin olarak saklanır:
///
/// <c>pbkdf2-sha256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;</c>
///
/// Yineleme sayısı özetin içinde durduğu için ileride artırılabilir; eski parolalar
/// kendi sayılarıyla doğrulanmaya devam eder.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>OWASP'ın PBKDF2-HMAC-SHA256 için önerdiği alt sınır (2023).</summary>
    public const int DefaultIterations = 600_000;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, iterations);

        return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Parolayı özetle karşılaştırır. Biçimi bozuk özetlerde istisna fırlatmaz, <c>false</c> döner —
    /// elle düzenlenmiş bir veritabanı satırı girişi kilitlemeli, sunucuyu çökertmemeli.
    /// </summary>
    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        // Sabit süreli karşılaştırma: erken çıkış, parolanın kaç karakterinin tuttuğunu sızdırır.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
}
