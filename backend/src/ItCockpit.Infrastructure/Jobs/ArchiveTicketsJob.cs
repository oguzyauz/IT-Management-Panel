using ItCockpit.Domain;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItCockpit.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job. Her gece 02:00'da çalışır ve <c>Completed</c> durumundaki
/// biletlerden tamamlanma tarihi üzerinden 14 gün geçmiş olanları <c>Archived</c> statüsüne çeker.
/// Böylece aktif listeler temiz kalır, kayıtlar ise raporlama için veritabanında korunur.
/// </summary>
public sealed class ArchiveTicketsJob
{
    public const string RecurringJobId = "ticket-auto-archive";
    private const int ArchiveAfterDays = 14;

    private readonly AppDbContext _db;
    private readonly ILogger<ArchiveTicketsJob> _logger;

    public ArchiveTicketsJob(AppDbContext db, ILogger<ArchiveTicketsJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Otomatik arşivleme job'u başladı.");

        var cutoff = DateTime.UtcNow.AddDays(-ArchiveAfterDays);

        var ticketsToArchive = await _db.Tickets
            .Where(t => t.Status == TicketStatus.Completed
                        && t.CompletedAtUtc != null
                        && t.CompletedAtUtc <= cutoff
                        && !t.IsDeleted)
            .ToListAsync(ct);

        if (ticketsToArchive.Count == 0)
        {
            _logger.LogInformation("Arşivlenecek bilet bulunamadı.");
            return;
        }

        foreach (var ticket in ticketsToArchive)
        {
            ticket.Status = TicketStatus.Archived;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Otomatik arşivleme job'u bitti: {Count} bilet arşivlendi (eşik: {Days} gün).",
            ticketsToArchive.Count, ArchiveAfterDays);
    }
}
