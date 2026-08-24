using ItCockpit.Domain;

namespace ItCockpit.Application.Contracts;

// --- DTO'lar ------------------------------------------------------------------------------------

/// <summary>İzin talebi detay DTO'su.</summary>
public sealed record LeaveRequestDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    LeaveType Type,
    LeaveStatus Status,
    string? Description,
    string? ReviewNote,
    Guid? ReviewedByUserId,
    string? ReviewedByName,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>Takvim görünümü için minimal izin bloğu.</summary>
public sealed record LeaveCalendarItemDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    DateOnly StartDate,
    DateOnly EndDate,
    LeaveType Type,
    LeaveStatus Status);

// --- İstekler -----------------------------------------------------------------------------------

public sealed record CreateLeaveRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    LeaveType Type,
    string? Description);

public sealed record LeaveDecisionRequest(
    LeaveStatus Decision,
    string? Note);
