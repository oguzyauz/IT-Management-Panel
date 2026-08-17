using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Parsing;
using ItCockpit.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Gmail;

/// <summary>
/// Gerçek Gmail API kaynağı.
/// <para>
/// Yalnızca <c>gmail.readonly</c> yetkisi ister ve sorguyu üç filtreyle daraltır:
/// ticket etiketi, izinli gönderen ve konu kalıbı. Kişisel gelen kutusunun tamamı okunmaz.
/// </para>
/// </summary>
public sealed partial class GmailTicketSource : IGmailTicketSource, IGmailAuthorizer
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailReadonly];

    private readonly GmailIngestionOptions _options;
    private readonly ILogger<GmailTicketSource> _logger;

    public GmailTicketSource(IOptions<GmailIngestionOptions> options, ILogger<GmailTicketSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Google";

    [GeneratedRegex(@"<a\s[^>]*href\s*=\s*[""'](?<href>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    public async Task<string> AuthorizeAsync(string mailbox, CancellationToken ct = default)
    {
        var service = await CreateServiceAsync(mailbox, allowInteractive: true, ct);
        var profile = await service.Users.GetProfile("me").ExecuteAsync(ct);

        _logger.LogInformation(
            "Gmail yetkilendirmesi tamamlandı. Beklenen kutu: {Expected}, yetki verilen: {Actual}",
            mailbox, profile.EmailAddress);

        return profile.EmailAddress ?? "(bilinmiyor)";
    }

    public async Task<IReadOnlyList<RawTicketMail>> FetchAsync(GmailFetchRequest request, CancellationToken ct = default)
    {
        var service = await CreateServiceAsync(request.MailboxAddress, allowInteractive: false, ct);
        var query = BuildQuery(request);

        _logger.LogInformation("Gmail sorgusu: {Query}", query);

        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = query;
        listRequest.MaxResults = request.MaxResults;

        if (!string.IsNullOrWhiteSpace(request.LabelName))
        {
            var labelId = await ResolveLabelIdAsync(service, request.LabelName, ct);
            if (labelId is not null)
                listRequest.LabelIds = new Google.Apis.Util.Repeatable<string>([labelId]);
            else
                _logger.LogWarning(
                    "Gmail etiketi bulunamadı: {Label}. Sorgu yalnızca gönderen ve konu filtresiyle çalışacak.",
                    request.LabelName);
        }

        var response = await listRequest.ExecuteAsync(ct);
        if (response.Messages is null || response.Messages.Count == 0)
            return [];

        var mails = new List<RawTicketMail>(response.Messages.Count);

        foreach (var summary in response.Messages)
        {
            ct.ThrowIfCancellationRequested();

            var getRequest = service.Users.Messages.Get("me", summary.Id);
            getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

            var message = await getRequest.ExecuteAsync(ct);
            mails.Add(ToRawMail(message));
        }

        return mails;
    }

    /// <summary>
    /// Gmail arama sorgusunu kurar.
    /// <para>
    /// <b>Önemli:</b> <c>from:</c> operatörü yalnızca <b>dış zarf</b> gönderenini eşler. Ticket maili
    /// bir çalışan tarafından iletildiğinde dış gönderen ileten kişidir; bu yüzden tek başına
    /// <c>from:ticket@menarini.com</c> kullanmak forward edilmiş ticket maillerini <b>tamamen kaçırır</b>.
    /// Adres ayrıca serbest metin olarak da aranır — böylece forward gövdesindeki
    /// <c>From: ticket@menarini.com</c> satırı da eşleşir. Gönderen doğrulaması yine de
    /// <see cref="ItCockpit.Application.Parsing.TicketMailParser"/> içindeki F1 filtresiyle,
    /// iç zarf üzerinden kesin olarak yapılır.
    /// </para>
    /// </summary>
    internal static string BuildQuery(GmailFetchRequest request)
    {
        var sb = new StringBuilder();

        if (request.AllowedSenders.Count > 0)
        {
            var clauses = request.AllowedSenders
                .SelectMany(s => new[] { $"from:{s}", $"\"{s}\"" });

            sb.Append('(').Append(string.Join(" OR ", clauses)).Append(") ");
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectContains))
            sb.Append("subject:\"").Append(request.SubjectContains).Append("\" ");

        // Gmail 'after:' yalnızca yyyy/MM/dd kabul eder. Türkçe kültürde '/' ayırıcısı '.' olarak
        // biçimlendiğinden InvariantCulture zorunludur ("2026.07.06" sorguyu bozar).
        if (request.SinceUtc is { } since)
            sb.Append("after:").Append(since.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)).Append(' ');

        return sb.ToString().Trim();
    }

    private static async Task<string?> ResolveLabelIdAsync(GmailService service, string labelName, CancellationToken ct)
    {
        var labels = await service.Users.Labels.List("me").ExecuteAsync(ct);
        return labels.Labels?
            .FirstOrDefault(l => string.Equals(l.Name, labelName, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    internal static RawTicketMail ToRawMail(Message message)
    {
        var headers = message.Payload?.Headers ?? [];

        string Header(string name) => headers
            .FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        var (plain, html) = ExtractBodies(message.Payload);

        var body = !string.IsNullOrWhiteSpace(plain) ? plain : HtmlToText(html);
        var links = ExtractLinks(html);

        var receivedAtUtc = message.InternalDate is { } ms
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
            : DateTime.UtcNow;

        return new RawTicketMail(
            message.Id ?? string.Empty,
            message.ThreadId ?? string.Empty,
            Header("Subject"),
            Header("From"),
            Header("To"),
            receivedAtUtc,
            body,
            links);
    }

    private static (string Plain, string Html) ExtractBodies(MessagePart? part)
    {
        if (part is null) return (string.Empty, string.Empty);

        var plain = new StringBuilder();
        var html = new StringBuilder();

        Walk(part);
        return (plain.ToString(), html.ToString());

        void Walk(MessagePart p)
        {
            var data = p.Body?.Data;
            if (!string.IsNullOrEmpty(data))
            {
                var decoded = DecodeBase64Url(data);
                if (string.Equals(p.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
                    plain.AppendLine(decoded);
                else if (string.Equals(p.MimeType, "text/html", StringComparison.OrdinalIgnoreCase))
                    html.AppendLine(decoded);
            }

            if (p.Parts is null) return;
            foreach (var child in p.Parts) Walk(child);
        }
    }

    private static string DecodeBase64Url(string data)
    {
        var s = data.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ExtractLinks(string html) =>
        string.IsNullOrWhiteSpace(html)
            ? []
            : AnchorRegex().Matches(html)
                .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups["href"].Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var withBreaks = html
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase);

        return System.Net.WebUtility.HtmlDecode(HtmlTagRegex().Replace(withBreaks, string.Empty));
    }

    /// <param name="mailbox">
    /// Token deposundaki anahtar. Her posta kutusunun kendi onayı ve kendi token'ı olur;
    /// bu değer karıştırılırsa yanlış kutu okunur.
    /// </param>
    /// <param name="mailbox">
    /// Token deposundaki anahtar. Her posta kutusunun kendi onayı ve kendi token'ı olur;
    /// bu değer karıştırılırsa yanlış kutu okunur.
    /// </param>
    /// <param name="allowInteractive">
    /// <c>false</c> ise onay ekranı <b>açılmaz</b>; token yoksa hata verilir.
    /// Mail okuma bu modda çalışır — zamanlanmış job'ın tarayıcı açıp asılması kabul edilemez,
    /// üstelik çoklu kutuda her yetkisiz kutu için ayrı pencere açılırdı.
    /// Onay yalnızca <see cref="AuthorizeAsync"/> üzerinden, kullanıcı isteğiyle alınır.
    /// </param>
    private async Task<GmailService> CreateServiceAsync(string mailbox, bool allowInteractive, CancellationToken ct)
    {
        var credentialsPath = GmailCredentialsInspector.ResolvePath(_options.CredentialsPath);

        if (!File.Exists(credentialsPath))
        {
            throw new InvalidOperationException(
                $"Gmail credentials dosyası bulunamadı: {credentialsPath}. " +
                "Durumu görmek için GET /api/ingestion/gmail-status çağırın.");
        }

        ClientSecrets secrets;
        await using (var stream = File.OpenRead(credentialsPath))
        {
            secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        }

        var dataStore = new FileDataStore(
            GmailCredentialsInspector.ResolvePath(_options.TokenStorePath), fullPath: true);

        UserCredential credential;

        if (allowInteractive)
        {
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets, Scopes, mailbox, ct, dataStore);
        }
        else
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = Scopes,
                DataStore = dataStore
            });

            var token = await dataStore.GetAsync<TokenResponse>(mailbox);

            if (token is null)
            {
                // Bu metin son kullanıcıya arayüzde olduğu gibi gösteriliyor; uç noktası
                // adı değil, kullanıcının yapacağı işlem yazılır.
                throw new InvalidOperationException(
                    $"'{mailbox}' kutusu için Google onayı verilmemiş. " +
                    "Yönetim ekranındaki Posta kutuları sekmesinden bu adresin yanındaki " +
                    "'Yetkilendir' düğmesine basıp kutunun sahibiyle Google girişini tamamlayın.");
            }

            // Süresi dolmuş erişim anahtarı refresh token ile sessizce yenilenir.
            credential = new UserCredential(flow, mailbox, token);
        }

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "IT Manager Cockpit"
        });
    }
}
