using ItCockpit.Domain;

namespace ItCockpit.Application.Contracts;

public sealed record MyWeekDto(
    Guid? WeekId,
    DateOnly WeekStartDate,
    ScheduleStatus Status,
    bool IsLocked,
    DateTime? LockDeadlineUtc,
    bool HasRuleViolation,
    string? RuleViolationNote,
    ScheduleRulesDto Rules,
    IReadOnlyList<MyWeekDayDto> Days,
    IReadOnlyList<ScheduleDecisionDto> Decisions);

public sealed record MyWeekDayDto(
    DateOnly Date, WorkMode? Mode, bool IsHoliday, string? HolidayName,
    bool IsManagerOverride, string? OverrideNote);

public sealed record ScheduleRulesDto(int RequiredOfficeDays, int RequiredHomeOfficeDays);

public sealed record ScheduleDecisionDto(
    Guid Id, ScheduleDecision Decision, Guid DecidedByUserId, string DecidedByName,
    DateTime DecidedAtUtc, string? Comment);

public sealed record SaveMyWeekRequest(DateOnly WeekStartDate, IReadOnlyList<SaveMyWeekDayRequest> Days);
public sealed record SaveMyWeekDayRequest(DateOnly Date, WorkMode Mode);

public sealed record ScheduleDecisionRequest(ScheduleDecision Decision, string? Comment);
public sealed record ScheduleOverrideRequest(DateOnly Date, WorkMode Mode, string? Note);
