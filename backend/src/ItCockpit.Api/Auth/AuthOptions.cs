namespace ItCockpit.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// <c>Local</c> (parola ile giriş — son kullanıcı kurulumlarının varsayılanı),
    /// <c>Mock</c> (geliştirme ve testler), <c>Google</c> (şirket SSO'su) veya
    /// <c>Ldap</c> (Active Directory doğrulaması).
    /// </summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Üretimde giriş yapabilecek şirket domainleri.</summary>
    public string[] AllowedDomains { get; set; } = ["menarini.com.tr"];

    /// <summary>Google OIDC istemci kimliği (Provider = Google iken zorunlu).</summary>
    public string? GoogleClientId { get; set; }

    public string GoogleAuthority { get; set; } = "https://accounts.google.com";
}

public static class AuthSchemes
{
    public const string Mock = "Mock";
    public const string Local = "Local";
    public const string Ldap = "Ldap";
}
