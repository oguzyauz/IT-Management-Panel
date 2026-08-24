using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Services;

/// <summary>
/// İzin talebi oluşturma, listeleme, onay/red ve iptal işlemleri.
/// </summary>
public sealed class LeaveService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public LeaveService(IAppDbContext db, IClock clock, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    // --- Listeleme ------------------------------------------------------------------------------

    /// <summary>
    /// Belirli bir tarih aralığındaki izinleri takvim için döner.
    /// Yalnızca onaylı ve bekleyen talepler gösterilir (iptal/red takvimde görünmez).
    /// </summary>
    public async Task<IReadOnlyList<LeaveCalendarItemDto>> GetCalendarAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var items = await _db.LeaveRequests
            .AsNoTracking()
            .Include(l => l.User)
            .Where(l => l.StartDate <= endDate && l.EndDate >= startDate)
            .Where(l => l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.Pending)
            .OrderBy(l => l.StartDate)
            .ThenBy(l => l.User.DisplayName)
            .ToListAsync(ct);

        return items.Select(l => new LeaveCalendarItemDto(
            l.Id, l.UserId, l.User.DisplayName,
            l.StartDate, l.EndDate, l.Type, l.Status)).ToList();
    }

    /// <summary>Kullanıcının kendi izin geçmişi (tüm durumlar).</summary>
    public async Task<IReadOnlyList<LeaveRequestDto>> GetMyLeavesAsync(
        Guid userId, CancellationToken ct = default)
    {
        var items = await _db.LeaveRequests
            .AsNoTracking()
            .Include(l => l.User)
            .Include(l => l.ReviewedByUser)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    /// <summary>Tüm izin taleplerini listele (yönetici görünümü, tarih filtreli).</summary>
    public async Task<IReadOnlyList<LeaveRequestDto>> GetAllLeavesAsync(
        DateOnly? startDate, DateOnly? endDate, LeaveStatus? status,
        CancellationToken ct = default)
    {
        var query = _db.LeaveRequests
            .AsNoTracking()
            .Include(l => l.User)
            .Include(l => l.ReviewedByUser)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(l => l.EndDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.StartDate <= endDate.Value);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    // --- Oluşturma ------------------------------------------------------------------------------

    /// <summary>
    /// Yeni izin talebi oluşturur. Aynı kullanıcının çakışan onaylı/bekleyen izni varsa engeller.
    /// </summary>
    public async Task<LeaveRequestDto> CreateAsync(
        Guid userId, CreateLeaveRequest request, CancellationToken ct = default)
    {
        if (request.EndDate < request.StartDate)
            throw new DomainRuleException("INVALID_DATE_RANGE", "Bitiş tarihi başlangıçtan önce olamaz.");

        if (request.StartDate < _clock.Today)
            throw new DomainRuleException("PAST_DATE", "Geçmiş tarihe izin talebi oluşturulamaz.");

        // Çakışma kontrolü
        var hasOverlap = await _db.LeaveRequests.AnyAsync(l =>
            l.UserId == userId
            && l.Status != LeaveStatus.Rejected
            && l.Status != LeaveStatus.Cancelled
            && l.StartDate <= request.EndDate
            && l.EndDate >= request.StartDate, ct);

        if (hasOverlap)
            throw new DomainRuleException("DATE_OVERLAP", "Seçilen tarih aralığında zaten bir izin talebiniz var.");

        var now = _clock.UtcNow;

        var leave = new LeaveRequest
        {
            UserId = userId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Type = request.Type,
            Status = LeaveStatus.Pending,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _db.LeaveRequests.Add(leave);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LEAVE_CREATED", "LeaveRequest", leave.Id.ToString(),
            new { leave.UserId, leave.StartDate, leave.EndDate, leave.Type }, ct);

        return await LoadDtoAsync(leave.Id, ct);
    }

    // --- Onay / Red -----------------------------------------------------------------------------

    /// <summary>Yönetici izin talebini onaylar veya reddeder.</summary>
    public async Task<LeaveRequestDto> DecideAsync(
        Guid leaveId, Guid reviewerUserId, LeaveDecisionRequest request, CancellationToken ct = default)
    {
        if (request.Decision != LeaveStatus.Approved && request.Decision != LeaveStatus.Rejected)
            throw new DomainRuleException("INVALID_DECISION", "Karar yalnızca Onay veya Red olabilir.");

        var leave = await _db.LeaveRequests
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == leaveId, ct)
            ?? throw new NotFoundException("İzin talebi bulunamadı.");

        if (leave.Status != LeaveStatus.Pending)
            throw new DomainRuleException("NOT_PENDING", "Yalnızca bekleyen talepler işlenebilir.");

        var now = _clock.UtcNow;

        leave.Status = request.Decision;
        leave.ReviewedByUserId = reviewerUserId;
        leave.ReviewedAtUtc = now;
        leave.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        leave.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            request.Decision == LeaveStatus.Approved ? "LEAVE_APPROVED" : "LEAVE_REJECTED",
            "LeaveRequest", leave.Id.ToString(),
            new { leave.UserId, Decision = request.Decision }, ct);

        return await LoadDtoAsync(leave.Id, ct);
    }

    // --- İptal ----------------------------------------------------------------------------------

    /// <summary>Kullanıcı kendi bekleyen talebini iptal eder.</summary>
    public async Task CancelAsync(Guid leaveId, Guid userId, CancellationToken ct = default)
    {
        var leave = await _db.LeaveRequests
            .FirstOrDefaultAsync(l => l.Id == leaveId && l.UserId == userId, ct)
            ?? throw new NotFoundException("İzin talebi bulunamadı.");

        if (leave.Status != LeaveStatus.Pending)
            throw new DomainRuleException("NOT_PENDING", "Yalnızca bekleyen talepler iptal edilebilir.");

        leave.Status = LeaveStatus.Cancelled;
        leave.UpdatedAtUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LEAVE_CANCELLED", "LeaveRequest", leave.Id.ToString(),
            new { leave.UserId }, ct);
    }

    // --- Yardımcılar ----------------------------------------------------------------------------

    private async Task<LeaveRequestDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var leave = await _db.LeaveRequests
            .AsNoTracking()
            .Include(l => l.User)
            .Include(l => l.ReviewedByUser)
            .FirstAsync(l => l.Id == id, ct);

        return ToDto(leave);
    }

    private static LeaveRequestDto ToDto(LeaveRequest l) => new(
        l.Id, l.UserId, l.User.DisplayName,
        l.StartDate, l.EndDate,
        CountBusinessDays(l.StartDate, l.EndDate),
        l.Type, l.Status, l.Description, l.ReviewNote,
        l.ReviewedByUserId, l.ReviewedByUser?.DisplayName,
        l.ReviewedAtUtc, l.CreatedAtUtc);

    /// <summary>Basit gün sayısı hesabı (hafta sonlarını çıkarır).</summary>
    private static int CountBusinessDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }
}
