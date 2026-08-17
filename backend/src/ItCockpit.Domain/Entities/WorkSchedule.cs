namespace ItCockpit.Domain.Entities;

public class WorkScheduleWeek
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Haftanın pazartesi günü (yerel tarih, saat bileşeni yok).</summary>
    public DateOnly WeekStartDate { get; set; }

    public ScheduleStatus Status { get; set; } = ScheduleStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }

    /// <summary>3 gün ofis / 2 gün home office kuralı ihlal edildi mi. Gönderimi engellemez.</summary>
    public bool HasRuleViolation { get; set; }
    public string? RuleViolationNote { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<WorkScheduleDay> Days { get; set; } = new List<WorkScheduleDay>();
    public ICollection<WorkScheduleApproval> Approvals { get; set; } = new List<WorkScheduleApproval>();
}

public class WorkScheduleDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkScheduleWeekId { get; set; }
    public DateOnly Date { get; set; }
    public WorkMode Mode { get; set; }

    public bool IsManagerOverride { get; set; }
    public Guid? OverriddenByUserId { get; set; }
    public string? OverrideNote { get; set; }

    public WorkScheduleWeek Week { get; set; } = null!;
}

public class WorkScheduleApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkScheduleWeekId { get; set; }
    public ScheduleDecision Decision { get; set; }
    public Guid DecidedByUserId { get; set; }
    public DateTime DecidedAtUtc { get; set; }
    public string? Comment { get; set; }

    public WorkScheduleWeek Week { get; set; } = null!;
}

public class WorkCalendarEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
    public WorkCalendarKind Kind { get; set; }
    public int Year { get; set; }
}
