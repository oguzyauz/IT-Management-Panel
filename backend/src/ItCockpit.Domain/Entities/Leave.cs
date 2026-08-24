namespace ItCockpit.Domain.Entities;

/// <summary>
/// İzin talebi. Çalışan oluşturur, yönetici onaylar/reddeder.
/// Kayıtlar silinmez; iptal edilen talepler <see cref="LeaveStatus.Cancelled"/> olarak işaretlenir.
/// </summary>
public class LeaveRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public LeaveType Type { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    /// <summary>Çalışanın izin sebebi notu (isteğe bağlı).</summary>
    public string? Description { get; set; }

    /// <summary>Yönetici red sebebi veya notu.</summary>
    public string? ReviewNote { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
