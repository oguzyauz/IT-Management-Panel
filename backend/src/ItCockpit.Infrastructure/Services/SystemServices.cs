using System.Text.Json;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Parsing;
using ItCockpit.Domain.Entities;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ItCockpit.Infrastructure.Services;

public sealed class AppClockOptions
{
    public const string SectionName = "Clock";
    public string TimeZone { get; set; } = "Europe/Istanbul";
}

public sealed class SystemClock : IClock
{
    public SystemClock(IOptions<AppClockOptions> options) =>
        LocalZone = MailDateParser.ResolveTimeZone(options.Value.TimeZone);

    public DateTime UtcNow => DateTime.UtcNow;
    public TimeZoneInfo LocalZone { get; }
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, LocalZone));
}

/// <summary>AppSettings tablosunu 60 saniyelik bellek önbelleğiyle okur.</summary>
public sealed class AppSettingsProvider : IAppSettingsProvider
{
    private const string CacheKey = "app-settings";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;

    public AppSettingsProvider(AppDbContext db, IMemoryCache cache, IClock clock)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : fallback;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        var values = await _db.AppSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase, ct);

        _cache.Set(CacheKey, (IReadOnlyDictionary<string, string>)values, CacheDuration);
        return values;
    }

    public async Task SetAsync(string key, string value, Guid? updatedByUserId, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null)
        {
            setting = new AppSetting { Key = key, Category = "General", DataType = "string" };
            _db.AppSettings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedAtUtc = _clock.UtcNow;
        setting.UpdatedByUserId = updatedByUserId;

        await _db.SaveChangesAsync(ct);
        Invalidate();
    }

    public void Invalidate() => _cache.Remove(CacheKey);
}

public sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AuditLogger(AppDbContext db, IClock clock, ICurrentUser currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string action, string entityType, string entityId, object? changes = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAtUtc = _clock.UtcNow,
            ActorUserId = _currentUser.UserId,
            ActorEmail = _currentUser.Email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes, JsonOptions)
        });

        await _db.SaveChangesAsync(ct);
    }
}
