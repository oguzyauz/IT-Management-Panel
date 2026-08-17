using ItCockpit.Application.Contracts;

namespace ItCockpit.Application.Services;

/// <summary>
/// Tixbox'tan SLA bilgisi gelmediği için hedef tarih/SLA <b>üretilmez</b>.
/// Bunun yerine AppSettings'ten okunan yaşlandırma eşikleri kullanılır ve arayüzde
/// "Uzun süredir açık" / "Güncelleme bekliyor" olarak gösterilir.
/// </summary>
public sealed record AgingThresholds(int StaleAfterDays, int OldAfterDays, int CriticalAfterDays)
{
    public static AgingThresholds Default { get; } = new(2, 5, 7);

    public AgingThresholdsDto ToDto() => new(StaleAfterDays, OldAfterDays, CriticalAfterDays);
}

public static class AgingCalculator
{
    public static int DaysOpen(DateTime originalSentAtUtc, DateTime nowUtc) =>
        Math.Max(0, (int)(nowUtc.Date - originalSentAtUtc.Date).TotalDays);

    public static int DaysSinceUpdate(DateTime updatedAtUtc, DateTime nowUtc) =>
        Math.Max(0, (int)(nowUtc.Date - updatedAtUtc.Date).TotalDays);

    public static AgingLevel Evaluate(
        DateTime originalSentAtUtc, DateTime updatedAtUtc, DateTime nowUtc, AgingThresholds thresholds)
    {
        var open = DaysOpen(originalSentAtUtc, nowUtc);
        var sinceUpdate = DaysSinceUpdate(updatedAtUtc, nowUtc);

        if (open >= thresholds.CriticalAfterDays) return AgingLevel.LongOpenCritical;
        if (open >= thresholds.OldAfterDays) return AgingLevel.LongOpen;
        if (sinceUpdate >= thresholds.StaleAfterDays) return AgingLevel.NeedsUpdate;
        return AgingLevel.Normal;
    }
}
