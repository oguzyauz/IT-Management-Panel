using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Gmail;

/// <summary>
/// Gerçek gönderim yapmayan sağlayıcı. Mail içeriğini loglar ve
/// <c>outbox</c> klasörüne <c>.eml</c> olarak yazar; böylece önizleme ve gönderim
/// akışı kimlik bilgisi olmadan uçtan uca denenebilir.
/// </summary>
public sealed class MockReminderMailSender : IReminderMailSender
{
    private readonly ILogger<MockReminderMailSender> _logger;
    private readonly IClock _clock;
    private readonly string _outboxPath;

    public MockReminderMailSender(ILogger<MockReminderMailSender> logger, IClock clock)
    {
        _logger = logger;
        _clock = clock;
        _outboxPath = Path.Combine(AppContext.BaseDirectory, "outbox");
    }

    public string ProviderName => "Mock";

    public async Task<ReminderSendResult> SendAsync(ReminderSendRequest request, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_outboxPath);

        var messageId = $"mock-{Guid.NewGuid():N}";
        var fileName = $"{_clock.UtcNow:yyyyMMdd-HHmmss}-{messageId}.eml";
        var path = Path.Combine(_outboxPath, fileName);

        var content = new StringBuilder()
            .AppendLine($"From: {request.FromAddress}")
            .AppendLine($"To: {request.ToAddress}")
            .AppendLine($"Cc: {string.Join(", ", request.CcAddresses)}")
            .AppendLine($"Subject: {request.Subject}")
            .AppendLine($"Date: {_clock.UtcNow:R}")
            .AppendLine()
            .AppendLine(request.Body)
            .ToString();

        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), ct);

        _logger.LogInformation(
            "[MOCK] Hatırlatma maili gönderildi (gerçek gönderim yapılmadı): {To} · {Subject} · {Path}",
            request.ToAddress, request.Subject, path);

        return new ReminderSendResult(true, messageId, null);
    }
}

/// <summary>
/// Gmail API ile gerçek gönderim. Yalnızca <c>gmail.send</c> yetkisi ister; okuma yetkisi
/// ayrı bir kimlik bağlamındadır (bkz. docs/revised-scope.md §9).
/// </summary>
public sealed class GmailReminderMailSender : IReminderMailSender
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailSend];

    private readonly GmailIngestionOptions _gmailOptions;
    private readonly ILogger<GmailReminderMailSender> _logger;

    public GmailReminderMailSender(
        IOptions<GmailIngestionOptions> gmailOptions, ILogger<GmailReminderMailSender> logger)
    {
        _gmailOptions = gmailOptions.Value;
        _logger = logger;
    }

    public string ProviderName => "Google";

    public async Task<ReminderSendResult> SendAsync(ReminderSendRequest request, CancellationToken ct = default)
    {
        try
        {
            var service = await CreateServiceAsync(ct);
            var raw = BuildRawMessage(request);

            var sent = await service.Users.Messages.Send(new Message { Raw = raw }, "me").ExecuteAsync(ct);
            return new ReminderSendResult(true, sent.Id, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hatırlatma maili gönderilemedi: {To}", request.ToAddress);
            return new ReminderSendResult(false, null, ex.Message);
        }
    }

    internal static string BuildRawMessage(ReminderSendRequest request)
    {
        var sb = new StringBuilder()
            .Append("From: ").Append(request.FromAddress).Append("\r\n")
            .Append("To: ").Append(request.ToAddress).Append("\r\n");

        if (request.CcAddresses.Count > 0)
            sb.Append("Cc: ").Append(string.Join(", ", request.CcAddresses)).Append("\r\n");

        // Türkçe karakterler için konu başlığı RFC 2047 ile kodlanır.
        sb.Append("Subject: =?UTF-8?B?")
          .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Subject)))
          .Append("?=\r\n")
          .Append("MIME-Version: 1.0\r\n")
          .Append("Content-Type: text/plain; charset=UTF-8\r\n")
          .Append("Content-Transfer-Encoding: base64\r\n\r\n")
          .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Body)));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private async Task<GmailService> CreateServiceAsync(CancellationToken ct)
    {
        var credentialsPath = GmailCredentialsInspector.ResolvePath(_gmailOptions.CredentialsPath);

        if (!File.Exists(credentialsPath))
        {
            throw new InvalidOperationException(
                $"Gmail credentials dosyası bulunamadı: {credentialsPath}. " +
                "Reminders:Provider ayarını 'Mock' yapın veya OAuth istemci dosyasını yerleştirin.");
        }

        await using var stream = File.OpenRead(credentialsPath);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            $"{_gmailOptions.MailboxAddress}:send",
            ct,
            new FileDataStore(_gmailOptions.TokenStorePath, fullPath: true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "IT Manager Cockpit"
        });
    }
}
