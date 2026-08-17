using System.Text.Json;
using ItCockpit.Application.Services;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Gmail;

/// <summary>
/// Gmail bağlantısının kuruluma hazır olup olmadığını raporlar.
/// <para>
/// <b>Sır sızdırmaz:</b> <c>client_secret</c> hiçbir koşulda okunmaz veya döndürülmez;
/// <c>client_id</c> yalnızca maskelenmiş hâlde gösterilir.
/// </para>
/// </summary>
public sealed class GmailCredentialsInspector
{
    private readonly GmailIngestionOptions _options;

    public GmailCredentialsInspector(IOptions<GmailIngestionOptions> options) => _options = options.Value;

    /// <param name="configuredMailboxes">
    /// Okunacak kutular. Liste artık veritabanından yönetildiği için (bkz. <c>MailboxRegistry</c>)
    /// dışarıdan verilir; denetçi dosya sistemine bakmakla sınırlıdır.
    /// </param>
    public GmailSetupStatus Inspect(IReadOnlyList<string> configuredMailboxes)
    {
        var credentialsPath = ResolvePath(_options.CredentialsPath);
        var tokenStorePath = ResolvePath(_options.TokenStorePath);

        // Google.Apis FileDataStore token dosyalarini "Google.Apis.Auth...-<kullanici>" adiyla saklar;
        // kutu adresi dosya adinin icinde gecer.
        var tokenFiles = Directory.Exists(tokenStorePath)
            ? Directory.EnumerateFiles(tokenStorePath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList()
            : [];

        var mailboxes = configuredMailboxes
            .Select(m => new MailboxAuthStatus(
                m,
                tokenFiles.Any(f => f.Contains(m, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var alreadyAuthorized = mailboxes.Count > 0 && mailboxes.All(m => m.Authorized);

        if (!File.Exists(credentialsPath))
        {
            return new GmailSetupStatus(
                Provider: _options.Provider,
                CredentialsPath: credentialsPath,
                CredentialsFound: false,
                CredentialsValid: false,
                Problem: "Google bağlantı dosyası (credentials.json) uygulama klasöründe yok. " +
                         "Bu dosya olmadan gerçek Gmail'e bağlanılamaz; IT sorumlunuzdan isteyin " +
                         "veya Google Cloud'dan 'Desktop app' tipinde bir OAuth istemcisi indirip " +
                         "uygulama klasörüne 'credentials.json' adıyla kopyalayın.",
                ClientType: null,
                ClientIdMasked: null,
                TokenStorePath: tokenStorePath,
                Mailboxes: mailboxes,
                AlreadyAuthorized: alreadyAuthorized,
                ReadyToAuthorize: false,
                NextStep: "credentials.json dosyasını yerleştirin.");
        }

        string? clientType = null;
        string? clientIdMasked = null;
        string? problem = null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(credentialsPath));
            var root = doc.RootElement;

            // Desktop app istemcisi "installed", web istemcisi "web" kökü ile gelir.
            if (root.TryGetProperty("installed", out var installed))
            {
                clientType = "installed";
                clientIdMasked = Mask(installed);
            }
            else if (root.TryGetProperty("web", out var web))
            {
                clientType = "web";
                clientIdMasked = Mask(web);
                problem = "Bu bir 'Web application' istemcisi. Masaüstü OAuth akışı için Google Cloud'da " +
                          "'Desktop app' tipinde yeni bir OAuth client ID oluşturun.";
            }
            else if (root.TryGetProperty("type", out var type)
                     && type.GetString() == "service_account")
            {
                clientType = "service_account";
                problem = "Bu bir servis hesabı anahtarı. Bu uygulama kullanıcı onaylı OAuth kullanır; " +
                          "'Desktop app' tipinde OAuth client ID indirin.";
            }
            else
            {
                problem = "JSON tanınmadı — 'installed' veya 'web' kökü yok. Yanlış dosya indirilmiş olabilir.";
            }
        }
        catch (JsonException ex)
        {
            problem = $"Dosya geçerli JSON değil: {ex.Message}";
        }
        catch (IOException ex)
        {
            problem = $"Dosya okunamadı: {ex.Message}";
        }

        var valid = clientType == "installed" && problem is null;
        var providerIsGoogle = string.Equals(_options.Provider, "Google", StringComparison.OrdinalIgnoreCase);

        var pending = mailboxes.Where(m => !m.Authorized).Select(m => m.MailboxAddress).ToList();

        var nextStep = !valid
            ? "credentials.json dosyası düzeltilmeli — aşağıdaki açıklamaya bakın."
            : !providerIsGoogle
                ? "Uygulama örnek mail dosyalarıyla çalışıyor. Gerçek Gmail'e bağlanmak için " +
                  "uygulama klasöründeki appsettings.Production.json dosyasında Gmail bölümündeki " +
                  "\"Provider\" değerini \"Google\" yapıp uygulamayı yeniden başlatın."
                : pending.Count == 0
                    ? "Hazır. Mailleri şimdi okutabilirsiniz."
                    : $"Yetkilendirilmemiş kutu(lar): {string.Join(", ", pending)}. " +
                      "Her biri için Yönetim ekranından 'Yetkilendir' düğmesine basın.";

        return new GmailSetupStatus(
            Provider: _options.Provider,
            CredentialsPath: credentialsPath,
            CredentialsFound: true,
            CredentialsValid: valid,
            Problem: problem,
            ClientType: clientType,
            ClientIdMasked: clientIdMasked,
            TokenStorePath: tokenStorePath,
            Mailboxes: mailboxes,
            AlreadyAuthorized: alreadyAuthorized,
            ReadyToAuthorize: valid && providerIsGoogle,
            NextStep: nextStep);
    }

    /// <summary>client_id'nin yalnızca tanınabilir kuyruğunu gösterir; client_secret'a hiç dokunulmaz.</summary>
    private static string? Mask(JsonElement section)
    {
        if (!section.TryGetProperty("client_id", out var idElement)) return null;

        var id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id)) return null;

        return id.Length <= 12 ? "***" : $"***{id[^12..]}";
    }

    /// <summary>
    /// Göreli yolları hem çalışma dizinine hem de uygulama çıktı klasörüne göre dener.
    /// Var olan ilk aday, yoksa çıktı klasörü adayı döner — böylece hata mesajı hep mutlak yol gösterir.
    /// </summary>
    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path))
        };

        return candidates.FirstOrDefault(c => File.Exists(c) || Directory.Exists(c)) ?? candidates[^1];
    }
}

public sealed record GmailSetupStatus(
    string Provider,
    string CredentialsPath,
    bool CredentialsFound,
    bool CredentialsValid,
    string? Problem,
    string? ClientType,
    string? ClientIdMasked,
    string TokenStorePath,
    IReadOnlyList<MailboxAuthStatus> Mailboxes,
    bool AlreadyAuthorized,
    bool ReadyToAuthorize,
    string NextStep);

/// <summary>Tek bir posta kutusunun yetkilendirme durumu.</summary>
public sealed record MailboxAuthStatus(string MailboxAddress, bool Authorized);
