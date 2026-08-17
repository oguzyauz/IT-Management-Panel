using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Parsing;
using ItCockpit.Infrastructure.Gmail;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ItCockpit.Tests;

public sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow, TimeZoneInfo? zone = null)
    {
        UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        LocalZone = zone ?? MailDateParser.ResolveTimeZone("Europe/Istanbul");
    }

    public DateTime UtcNow { get; set; }
    public TimeZoneInfo LocalZone { get; }
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, LocalZone));

    /// <summary>Oturum süresi, hesap kilidi gibi zamana bağlı kuralları sınamak için.</summary>
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public bool IsInRole(string role) => Roles.Contains(role);
}

public sealed class TestAuditLogger : IAuditLogger
{
    public List<(string Action, string EntityType, string EntityId)> Entries { get; } = [];

    public Task LogAsync(string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
    {
        Entries.Add((action, entityType, entityId));
        return Task.CompletedTask;
    }
}

public sealed class TestAppSettingsProvider : IAppSettingsProvider
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public TestAppSettingsProvider(IDictionary<string, string>? values = null)
    {
        if (values is null) return;
        foreach (var (k, v) in values) _values[k] = v;
    }

    public Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        Task.FromResult(_values.TryGetValue(key, out var raw) && int.TryParse(raw, out var v) ? v : fallback);

    public Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default) =>
        Task.FromResult(_values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback);

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(_values);

    public Task SetAsync(string key, string value, Guid? updatedByUserId, CancellationToken ct = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public void Invalidate() { }
}

/// <summary>
/// Bellekteki mail listesini döndüren test kaynağı.
/// <para>
/// <see cref="Mails"/> tek kutulu testler için ortak listedir. Çoklu kutu testlerinde
/// <see cref="MailsPerMailbox"/> kullanılır; bir kutu için giriş varsa o öncelikli olur.
/// </para>
/// </summary>
public sealed class StubGmailTicketSource : IGmailTicketSource
{
    public List<RawTicketMail> Mails { get; } = [];

    public Dictionary<string, List<RawTicketMail>> MailsPerMailbox { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Erişilemeyen kutuları taklit eder (ör. yetkilendirilmemiş hesap).</summary>
    public HashSet<string> FailingMailboxes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gelen istekler — okuma penceresinin (<c>SinceUtc</c>) doğruluğunu sınamak için.</summary>
    public List<GmailFetchRequest> Requests { get; } = [];

    public string ProviderName => "Stub";

    public Task<IReadOnlyList<RawTicketMail>> FetchAsync(GmailFetchRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);

        if (FailingMailboxes.Contains(request.MailboxAddress))
            throw new InvalidOperationException($"Kutuya erişilemedi: {request.MailboxAddress}");

        var mails = MailsPerMailbox.TryGetValue(request.MailboxAddress, out var scoped) ? scoped : Mails;
        return Task.FromResult<IReadOnlyList<RawTicketMail>>(mails);
    }
}

public sealed class StubReminderMailSender : IReminderMailSender
{
    public List<ReminderSendRequest> Sent { get; } = [];
    public bool ShouldFail { get; set; }

    public string ProviderName => "Stub";

    public Task<ReminderSendResult> SendAsync(ReminderSendRequest request, CancellationToken ct = default)
    {
        if (ShouldFail)
            return Task.FromResult(new ReminderSendResult(false, null, "stub failure"));

        Sent.Add(request);
        return Task.FromResult(new ReminderSendResult(true, $"stub-{Sent.Count}", null));
    }
}

public static class Fixtures
{
    public static string Directory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string ReadText(string fileName) =>
        File.ReadAllText(Path.Combine(Directory, fileName));

    /// <summary>Fixture dosyasını <see cref="RawTicketMail"/> hâline getirir.</summary>
    public static RawTicketMail Load(string fileName, IClock? clock = null)
    {
        var source = new MockGmailTicketSource(
            Options.Create(new MockGmailOptions()),
            NullLogger<MockGmailTicketSource>.Instance,
            clock ?? new FixedClock(new DateTime(2026, 8, 4, 9, 0, 0)));

        return source.ParseFixture(ReadText(fileName), Path.GetFileNameWithoutExtension(fileName))
               ?? throw new InvalidOperationException($"Fixture okunamadı: {fileName}");
    }

    public static TicketMailParser CreateParser(TicketMailParserOptions? options = null) =>
        new(options ?? new TicketMailParserOptions());
}
