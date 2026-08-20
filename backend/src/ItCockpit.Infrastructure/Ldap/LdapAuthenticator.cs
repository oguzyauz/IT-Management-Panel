using System.DirectoryServices.Protocols;
using System.Net;
using ItCockpit.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Ldap;

/// <summary>
/// Active Directory / LDAP ile kimlik doğrulama. Kullanıcının girdiği e-posta ve parola ile
/// LDAP Simple Bind yapılır. Başarılıysa opsiyonel olarak <c>displayName</c> özniteliği okunur.
/// </summary>
public sealed class LdapAuthenticator : ILdapAuthenticator
{
    private readonly LdapOptions _options;
    private readonly ILogger<LdapAuthenticator> _logger;

    public LdapAuthenticator(IOptions<LdapOptions> options, ILogger<LdapAuthenticator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<LdapAuthResult> AuthenticateAsync(
        string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Server))
            return Task.FromResult(new LdapAuthResult(false, ErrorMessage: "LDAP sunucusu yapılandırılmamış."));

        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult(new LdapAuthResult(false, ErrorMessage: "Parola boş olamaz."));

        try
        {
            var identifier = new LdapDirectoryIdentifier(_options.Server, _options.Port);
            var credential = new NetworkCredential(email, password);

            using var connection = new LdapConnection(identifier);
            connection.AuthType = AuthType.Basic;
            connection.SessionOptions.ProtocolVersion = 3;

            if (_options.UseSsl)
                connection.SessionOptions.SecureSocketLayer = true;

            // Simple Bind — kullanıcının kendi kimlik bilgileriyle doğrulama
            connection.Bind(credential);

            // Bind başarılı → opsiyonel olarak DisplayName okunur
            string? displayName = null;
            if (_options.SearchBase is not null)
            {
                try
                {
                    var filter = string.Format(_options.SearchFilter, EscapeLdapFilter(email));
                    var searchRequest = new SearchRequest(
                        _options.SearchBase,
                        filter,
                        SearchScope.Subtree,
                        "displayName", "cn");

                    if (connection.SendRequest(searchRequest) is SearchResponse response
                        && response.Entries.Count > 0)
                    {
                        var entry = response.Entries[0];
                        displayName = GetAttribute(entry, "displayName")
                                      ?? GetAttribute(entry, "cn");
                    }
                }
                catch (Exception ex)
                {
                    // DisplayName okunamazsa giriş yine kabul edilir.
                    _logger.LogWarning(ex, "LDAP displayName okunamadı: {Email}", email);
                }
            }

            _logger.LogInformation("LDAP doğrulama başarılı: {Email}", email);
            return Task.FromResult(new LdapAuthResult(true, displayName));
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "LDAP doğrulama başarısız: {Email} — {Message}", email, ex.Message);
            return Task.FromResult(new LdapAuthResult(false, ErrorMessage: "LDAP doğrulama başarısız."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP bağlantı hatası: {Email}", email);
            return Task.FromResult(new LdapAuthResult(false, ErrorMessage: "LDAP sunucusuna bağlanılamadı."));
        }
    }

    private static string? GetAttribute(SearchResultEntry entry, string name) =>
        entry.Attributes.Contains(name) && entry.Attributes[name].Count > 0
            ? entry.Attributes[name][0]?.ToString()
            : null;

    /// <summary>LDAP injection'ı önlemek için özel karakterleri escape'ler.</summary>
    private static string EscapeLdapFilter(string value) =>
        value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
}
