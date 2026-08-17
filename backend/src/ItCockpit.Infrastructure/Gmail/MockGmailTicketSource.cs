using System.Text.RegularExpressions;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Gmail;

public sealed class MockGmailOptions
{
    public const string SectionName = "MockGmail";

    /// <summary>Fixture <c>.txt</c> dosyalarının bulunduğu klasör (uygulama köküne göre).</summary>
    public string FixtureDirectory { get; set; } = "MailFixtures";
}

/// <summary>
/// Gerçek Gmail kimlik bilgisi olmadan çalışan geliştirme kaynağı.
/// Fixture dosyalarını RFC822 benzeri basit bir formatta okur:
/// başlık satırları, boş satır, gövde.
/// </summary>
public sealed partial class MockGmailTicketSource : IGmailTicketSource
{
    private readonly MockGmailOptions _options;
    private readonly ILogger<MockGmailTicketSource> _logger;
    private readonly IClock _clock;

    public MockGmailTicketSource(
        IOptions<MockGmailOptions> options, ILogger<MockGmailTicketSource> logger, IClock clock)
    {
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public string ProviderName => "Mock";

    [GeneratedRegex(@"^(?<key>[A-Za-z-]+):[ \t]*(?<value>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"https?://[^\s<>""')\]]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();

    public Task<IReadOnlyList<RawTicketMail>> FetchAsync(GmailFetchRequest request, CancellationToken ct = default)
    {
        var dir = Path.IsPathRooted(_options.FixtureDirectory)
            ? _options.FixtureDirectory
            : Path.Combine(AppContext.BaseDirectory, _options.FixtureDirectory);

        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Mock Gmail fixture klasörü bulunamadı: {Dir}", dir);
            return Task.FromResult<IReadOnlyList<RawTicketMail>>([]);
        }

        var mails = new List<RawTicketMail>();

        foreach (var file in Directory.EnumerateFiles(dir, "*.txt").OrderBy(f => f))
        {
            ct.ThrowIfCancellationRequested();

            var text = File.ReadAllText(file);
            var mail = ParseFixture(text, Path.GetFileNameWithoutExtension(file));
            if (mail is not null) mails.Add(mail);
        }

        _logger.LogInformation("Mock Gmail kaynağından {Count} mail okundu ({Dir})", mails.Count, dir);
        return Task.FromResult<IReadOnlyList<RawTicketMail>>(mails.Take(request.MaxResults).ToList());
    }

    public RawTicketMail? ParseFixture(string text, string fallbackId)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var separatorIndex = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        if (separatorIndex < 0) return null;

        var headerBlock = normalized[..separatorIndex];
        var body = normalized[(separatorIndex + 2)..];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in headerBlock.Split('\n'))
        {
            var m = HeaderRegex().Match(line.Trim());
            if (m.Success) headers[m.Groups["key"].Value] = m.Groups["value"].Value.Trim();
        }

        var messageId = headers.GetValueOrDefault("Message-Id", fallbackId);
        var threadId = headers.GetValueOrDefault("Thread-Id", messageId);

        var receivedAtUtc = MailDateParser.TryParse(headers.GetValueOrDefault("Date"), _clock.LocalZone, out var utc)
            ? utc
            : _clock.UtcNow;

        var links = UrlRegex().Matches(body).Select(m => m.Value).Distinct().ToList();

        return new RawTicketMail(
            messageId,
            threadId,
            headers.GetValueOrDefault("Subject", string.Empty),
            headers.GetValueOrDefault("From", string.Empty),
            headers.GetValueOrDefault("To", string.Empty),
            receivedAtUtc,
            body,
            links);
    }
}
