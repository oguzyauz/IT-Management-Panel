namespace ItCockpit.Application.Abstractions;

/// <summary>
/// LDAP/Active Directory kimlik doğrulama soyutlaması.
/// Implementasyonu Infrastructure katmanındadır; Application katmanı yalnızca bu arayüzü bilir.
/// </summary>
public interface ILdapAuthenticator
{
    Task<LdapAuthResult> AuthenticateAsync(
        string email, string password, CancellationToken ct = default);
}

public sealed record LdapAuthResult(
    bool Success,
    string? DisplayName = null,
    string? ErrorMessage = null);

/// <summary>LDAP bağlantı ayarları. <c>appsettings.json</c> içindeki <c>Auth</c> bölümünden okunur.</summary>
public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    /// <summary>LDAP sunucu adresi (örn. dc.menarini.com.tr).</summary>
    public string? Server { get; set; }

    /// <summary>LDAP bağlantı portu. Varsayılan 389 (LDAP), SSL için genellikle 636.</summary>
    public int Port { get; set; } = 389;

    /// <summary>LDAPS (SSL/TLS) kullanılıp kullanılmayacağı.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>Kullanıcı arama kök DN'i (örn. DC=menarini,DC=com,DC=tr).</summary>
    public string? SearchBase { get; set; }

    /// <summary>
    /// Kullanıcıyı bulmak için LDAP filtresi. <c>{0}</c> e-posta adresiyle değiştirilir.
    /// </summary>
    public string SearchFilter { get; set; } = "(userPrincipalName={0})";

    /// <summary>Arama için kullanılacak servis hesabı DN'i (opsiyonel).</summary>
    public string? BindDn { get; set; }

    /// <summary>Servis hesabı parolası (opsiyonel).</summary>
    public string? BindPassword { get; set; }
}
