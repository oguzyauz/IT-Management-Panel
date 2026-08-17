namespace ItCockpit.Domain;

/// <summary>
/// Panel içi durum geçiş matrisi. Bu geçişler <b>yalnızca yönetim panelini</b> ilgilendirir;
/// Tixbox'ta hiçbir karşılığı yoktur ve Tixbox'a yazılmaz.
/// </summary>
public static class TicketStatusTransitions
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Allowed =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.New] = [TicketStatus.Unassigned, TicketStatus.Assigned],
            [TicketStatus.Unassigned] = [TicketStatus.Assigned],
            [TicketStatus.Assigned] = [TicketStatus.InProgress, TicketStatus.Completed, TicketStatus.Unassigned, TicketStatus.Assigned],
            [TicketStatus.InProgress] = [TicketStatus.Completed, TicketStatus.Assigned],
            [TicketStatus.Completed] = [TicketStatus.Archived, TicketStatus.InProgress],
            [TicketStatus.Archived] = [TicketStatus.Completed]
        };

    public static bool IsAllowed(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<TicketStatus> AllowedFrom(TicketStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Dashboard'daki "açık ticket" tanımı.</summary>
    public static bool IsOpen(TicketStatus status) =>
        status is TicketStatus.New or TicketStatus.Unassigned or TicketStatus.Assigned or TicketStatus.InProgress;

    /// <summary>
    /// Çalışanın <b>kendi</b> ticket'ında yapabileceği geçişler.
    /// <para>
    /// İleri yönde ilerletebildiği gibi <b>geri de alabilir</b>: tamamladığını yeniden açabilir
    /// (<c>COMPLETED → IN_PROGRESS</c>) ve başladığı işi bekletmeye alabilir
    /// (<c>IN_PROGRESS → ASSIGNED</c>). Yanlış tıklamayı düzeltmek için müdüre başvurmak zorunda
    /// kalmaması gerekir.
    /// </para>
    /// <para>
    /// Müdürde kalanlar: atamayı geri verme (<see cref="TicketStatus.Unassigned"/>) ve arşivleme.
    /// Çalışan kendi işini sıraya geri atamaz.
    /// </para>
    /// </summary>
    private static readonly TicketStatus[] EmployeeAllowedTargets =
        [TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Completed];

    public static bool IsAllowedForEmployee(TicketStatus from, TicketStatus to) =>
        EmployeeAllowedTargets.Contains(to) && IsAllowed(from, to);

    public static IReadOnlyList<TicketStatus> AllowedFromForEmployee(TicketStatus from) =>
        AllowedFrom(from).Where(s => EmployeeAllowedTargets.Contains(s)).ToArray();
}
