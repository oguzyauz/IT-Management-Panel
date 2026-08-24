namespace ItCockpit.Domain;

/// <summary>
/// Panel içi takip durumu. Tixbox durumundan tamamen bağımsızdır ve Tixbox'a yazılmaz.
/// </summary>
public enum TicketStatus
{
    /// <summary>Okundu ama henüz işlenmedi. Ingestion bu değeri kullanmaz; ileriye dönük ayrılmıştır.</summary>
    New = 0,
    Unassigned = 1,
    Assigned = 2,
    InProgress = 3,
    Completed = 4,
    Archived = 5
}

public enum TicketType
{
    Incident = 1,
    ServiceRequest = 2
}

public enum WorkMode
{
    Office = 1,
    HomeOffice = 2,
    Leave = 3
}

public enum ScheduleStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}

public enum ScheduleDecision
{
    Approved = 1,
    Rejected = 2
}

public enum ReminderStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public enum ParseWarningSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public enum WorkCalendarKind
{
    PublicHoliday = 1,
    HalfDay = 2,
    CompanyHoliday = 3
}

/// <summary>İzin türü.</summary>
public enum LeaveType
{
    /// <summary>Yıllık izin.</summary>
    Annual = 1,
    /// <summary>Mazeret izni.</summary>
    Personal = 2,
    /// <summary>Hastalık / rapor.</summary>
    Medical = 3,
    /// <summary>Ücretsiz izin.</summary>
    Unpaid = 4
}

/// <summary>İzin talebi durumu.</summary>
public enum LeaveStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Employee = "EMPLOYEE";
}
