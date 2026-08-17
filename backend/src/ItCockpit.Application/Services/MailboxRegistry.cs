using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItCockpit.Application.Services;

/// <summary>
/// Okunacak posta kutularının tek doğruluk kaynağı.
///
/// Liste veritabanındaki <see cref="AppSettingKeys.GmailMailboxes"/> ayarında durur; böylece
/// yönetici arayüzden kutu ekleyip çıkarabilir. Ayar hiç yazılmamışsa
/// <c>appsettings.json</c>'daki listeye düşülür — mevcut kurulumlar bozulmaz.
/// </summary>
public sealed class MailboxRegistry
{
    private readonly IAppDbContext _db;
    private readonly IAppSettingsProvider _settings;
    private readonly GmailIngestionOptions _options;

    public MailboxRegistry(
        IAppDbContext db, IAppSettingsProvider settings, IOptions<GmailIngestionOptions> options)
    {
        _db = db;
        _settings = settings;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken ct = default)
    {
        var stored = await _settings.GetStringAsync(AppSettingKeys.GmailMailboxes, string.Empty, ct);

        var fromSettings = Parse(stored);
        return fromSettings.Count > 0 ? fromSettings : _options.EffectiveMailboxes;
    }

    /// <summary>
    /// Kutu ekler. Yetkilendirme <b>yapmaz</b> — kutu eklendikten sonra ayrıca
    /// <c>POST /api/ingestion/authorize</c> ile Google onayı alınmalıdır. Yetkisiz kutu
    /// her okumada kalıcı hata üretir, bu yüzden ekleme ve yetkilendirme ayrı adımlardır.
    /// </summary>
    public async Task<IReadOnlyList<string>> AddAsync(string mailbox, CancellationToken ct = default)
    {
        var normalized = Normalize(mailbox);

        if (normalized.Length == 0 || !normalized.Contains('@'))
            throw new DomainRuleException("INVALID_MAILBOX", "Geçerli bir e-posta adresi girin.");

        var current = (await GetAsync(ct)).ToList();

        if (current.Any(m => string.Equals(m, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new DomainRuleException("DUPLICATE_MAILBOX", "Bu posta kutusu zaten ekli.");

        current.Add(normalized);
        await SaveAsync(current, ct);

        return current;
    }

    /// <summary>
    /// Kutuyu listeden çıkarır. Daha önce o kutudan okunmuş ticket'lar <b>silinmez</b>;
    /// yalnızca bundan sonra okunmaz.
    ///
    /// Kutunun senkron durumu da silinir. Böylece kutu tekrar eklendiğinde okuma penceresi
    /// baştan başlar — kullanıcının elinde, kod değiştirmeden bir "baştan tara" yolu olur.
    /// </summary>
    public async Task<IReadOnlyList<string>> RemoveAsync(string mailbox, CancellationToken ct = default)
    {
        var normalized = Normalize(mailbox);
        var current = await GetAsync(ct);

        var remaining = current
            .Where(m => !string.Equals(m, normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remaining.Count == current.Count)
            throw new NotFoundException($"Posta kutusu listede yok: {mailbox}");

        var states = await _db.GmailSyncStates
            .Where(s => s.MailboxAddress == normalized)
            .ToListAsync(ct);

        _db.GmailSyncStates.RemoveRange(states);
        await _db.SaveChangesAsync(ct);

        await SaveAsync(remaining, ct);
        return remaining;
    }

    /// <summary>
    /// Kutunun okuma penceresini sıfırlar: senkron durumu silinir, sonraki okuma ilk kez
    /// okunuyormuş gibi <c>InitialLookbackDays</c> kadar geriye bakar.
    ///
    /// Kutu bağlı ve hatasız göründüğü hâlde eski mailler gelmiyorsa kullanıcının elindeki
    /// çözüm budur. Ticket'lar silinmez; zaten okunmuş mailler duplicate korumasına takılır.
    /// </summary>
    public async Task ResetSyncStateAsync(string mailbox, CancellationToken ct = default)
    {
        var normalized = Normalize(mailbox);
        var current = await GetAsync(ct);

        if (!current.Any(m => string.Equals(m, normalized, StringComparison.OrdinalIgnoreCase)))
            throw new NotFoundException($"Posta kutusu listede yok: {mailbox}");

        var states = await _db.GmailSyncStates
            .Where(s => s.MailboxAddress == normalized)
            .ToListAsync(ct);

        _db.GmailSyncStates.RemoveRange(states);
        await _db.SaveChangesAsync(ct);
    }

    private Task SaveAsync(IReadOnlyList<string> mailboxes, CancellationToken ct) =>
        _settings.SetAsync(AppSettingKeys.GmailMailboxes, string.Join('\n', mailboxes), null, ct);

    private static List<string> Parse(string? raw) =>
        (raw ?? string.Empty)
        .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(Normalize)
        .Where(m => m.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>Türkçe kültürde <c>ToLower()</c> "I" harfini bozar; kültürden bağımsız küçültülür.</summary>
    private static string Normalize(string? mailbox) => (mailbox ?? string.Empty).Trim().ToLowerInvariant();
}
