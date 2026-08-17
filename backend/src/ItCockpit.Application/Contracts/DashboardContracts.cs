using ItCockpit.Domain;

namespace ItCockpit.Application.Contracts;

public sealed record DashboardDto(
    DashboardMetricsDto Metrics,
    TodayTeamStatusDto TodayTeamStatus,
    IReadOnlyList<TicketListItemDto> UnassignedTickets,
    IReadOnlyList<TicketListItemDto> AttentionTickets,
    IReadOnlyList<EmployeeWorkloadDto> Workload,
    WeeklyScheduleMatrixDto WeeklyMatrix,
    IReadOnlyList<ReminderHistoryItemDto> RecentReminders,
    IReadOnlyList<ParseWarningDto> DataMismatchWarnings,
    IReadOnlyList<TeamStatusUpdateDto> RecentTeamUpdates,
    AgingThresholdsDto AgingThresholds);

/// <summary>
/// Çalışanların kendi ticket'larında yaptığı durum değişiklikleri. Müdürün ekibin ne yaptığını
/// sormadan görebilmesi için dashboard'da listelenir.
/// </summary>
public sealed record TeamStatusUpdateDto(
    Guid TicketId,
    string ExternalTicketNumber,
    string ApplicationName,
    Guid ChangedByUserId,
    string ChangedByName,
    TicketStatus? FromStatus,
    TicketStatus ToStatus,
    DateTime ChangedAtUtc,
    string? Note);

public sealed record DashboardMetricsDto(
    int TotalOpenTickets,
    int UnassignedTickets,
    int InProgressTickets,
    // Aging eşiğini aşmış (uzun süredir açık / güncelleme bekleyen) ticket sayısı.
    int StaleTickets,
    int InOfficeToday,
    int HomeOfficeToday,
    int OnLeaveToday,
    int MissingScheduleSubmissions);

public sealed record AgingThresholdsDto(int StaleAfterDays, int OldAfterDays, int CriticalAfterDays);

public sealed record TodayTeamStatusDto(
    DateOnly Date,
    bool IsHoliday,
    string? HolidayName,
    IReadOnlyList<TeamMemberDayStatusDto> Members);

public sealed record TeamMemberDayStatusDto(
    Guid UserId, string DisplayName, string? Title, WorkMode? Mode, bool HasSubmittedWeek);

public sealed record EmployeeWorkloadDto(
    Guid UserId, string DisplayName, string? Title,
    int OpenTicketCount, int InProgressCount, int StaleCount, WorkMode? TodayMode);

public sealed record WeeklyScheduleMatrixDto(
    DateOnly WeekStartDate,
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<WeeklyScheduleRowDto> Rows);

public sealed record WeeklyScheduleRowDto(
    Guid UserId,
    string DisplayName,
    Guid? WeekId,
    ScheduleStatus Status,
    bool HasRuleViolation,
    string? RuleViolationNote,
    IReadOnlyList<WeeklyScheduleCellDto> Cells);

public sealed record WeeklyScheduleCellDto(
    DateOnly Date, WorkMode? Mode, bool IsManagerOverride, bool IsHoliday, string? HolidayName);
