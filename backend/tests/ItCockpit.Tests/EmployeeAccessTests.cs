using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Çalışan yalnızca kendine atanmış ticket'ları görebilir ve sınırlı durum geçişleri yapabilir.
/// Kapsam daraltması sunucu tarafında zorlanır; istemcinin gönderdiği filtreye güvenilmez.
/// </summary>
public sealed class EmployeeAccessTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    /// <summary>İki ticket oluşturur: biri Dilara'ya atanmış, biri Beril'e.</summary>
    private async Task<(Guid Mine, Guid Others)> SeedTwoAssignedTicketsAsync()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        _h.GmailSource.Mails.Add(Fixtures.Load("direct-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        var mine = await _h.Db.Tickets
            .Where(t => t.ExternalTicketNumber == "I260729_000144").Select(t => t.Id).SingleAsync();
        var others = await _h.Db.Tickets
            .Where(t => t.ExternalTicketNumber == "S260801_000210").Select(t => t.Id).SingleAsync();

        await _h.Tickets.AssignAsync(mine, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));
        await _h.Tickets.AssignAsync(others, new AssignTicketRequest(ServiceTestHarness.Employee2Id, null));

        return (mine, others);
    }

    private void ActAsEmployee() => _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

    // -----------------------------------------------------------------------------------
    // Görünürlük
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Employee_search_returns_only_own_tickets()
    {
        await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var result = await _h.Tickets.SearchAsync(new TicketQuery());

        result.TotalCount.Should().Be(1);
        result.Items.Single().ExternalTicketNumber.Should().Be("I260729_000144");
    }

    [Fact]
    public async Task Employee_cannot_widen_scope_by_passing_another_assignee_filter()
    {
        await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        // İstemci başkasının kimliğini göndermeyi denese bile kapsam daralması kazanır:
        // iki filtre kesişir ve sonuç boş kalır — yabancı kayıt hiçbir şekilde sızmaz.
        var foreign = await _h.Tickets.SearchAsync(
            new TicketQuery(AssigneeUserId: ServiceTestHarness.Employee2Id));

        foreign.TotalCount.Should().Be(0);
        foreign.Items.Should().NotContain(t => t.AssigneeUserId == ServiceTestHarness.Employee2Id);

        // Kendi kapsamı ise etkilenmez.
        var own = await _h.Tickets.SearchAsync(new TicketQuery());
        own.Items.Should().ContainSingle()
            .Which.AssigneeUserId.Should().Be(ServiceTestHarness.EmployeeId);
    }

    [Fact]
    public async Task Employee_cannot_see_unassigned_tickets()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        ActAsEmployee();
        var result = await _h.Tickets.SearchAsync(new TicketQuery(Unassigned: true));

        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Employee_can_open_own_ticket_detail()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var detail = await _h.Tickets.GetAsync(mine);

        detail.ExternalTicketNumber.Should().Be("I260729_000144");
        detail.AssigneeUserId.Should().Be(ServiceTestHarness.EmployeeId);
    }

    [Fact]
    public async Task Employee_cannot_open_someone_elses_ticket_even_with_direct_id()
    {
        var (_, others) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var act = () => _h.Tickets.GetAsync(others);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // -----------------------------------------------------------------------------------
    // Durum değiştirme
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Employee_can_move_own_ticket_to_in_progress_and_completed()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var inProgress = await _h.Tickets.ChangeStatusAsync(
            mine, new ChangeStatusRequest(TicketStatus.InProgress, "Bakıyorum"));
        inProgress.Status.Should().Be(TicketStatus.InProgress);

        var completed = await _h.Tickets.ChangeStatusAsync(
            mine, new ChangeStatusRequest(TicketStatus.Completed, "Çözüldü"));
        completed.Status.Should().Be(TicketStatus.Completed);
        completed.CompletedAtUtc.Should().NotBeNull();
    }

    // Geri alma: çalışan kendi hatasını düzeltmek için müdüre başvurmak zorunda kalmamalı.

    [Fact]
    public async Task Employee_can_reopen_a_ticket_they_completed()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.Completed, null));

        var reopened = await _h.Tickets.ChangeStatusAsync(
            mine, new ChangeStatusRequest(TicketStatus.InProgress, "Yanlışlıkla kapatmışım"));

        reopened.Status.Should().Be(TicketStatus.InProgress);
        reopened.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Employee_can_move_a_started_ticket_back_to_assigned()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));

        var paused = await _h.Tickets.ChangeStatusAsync(
            mine, new ChangeStatusRequest(TicketStatus.Assigned, "Bilgi bekliyorum, beklemeye aldım"));

        paused.Status.Should().Be(TicketStatus.Assigned);
        paused.AssigneeUserId.Should().Be(ServiceTestHarness.EmployeeId);   // atama korunur
    }

    [Fact]
    public async Task Employee_backward_transitions_are_recorded_in_history()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));

        // Sabit test saati ilerletilir; aksi hâlde iki geçiş aynı zaman damgasını alır ve
        // geçmiş sıralaması belirsizleşir.
        _h.Clock.UtcNow = _h.Clock.UtcNow.AddMinutes(5);

        var back = await _h.Tickets.ChangeStatusAsync(
            mine, new ChangeStatusRequest(TicketStatus.Assigned, "Beklemeye aldım"));

        var entry = back.StatusHistory.Should()
            .ContainSingle(h => h.ToStatus == TicketStatus.Assigned && h.FromStatus == TicketStatus.InProgress)
            .Subject;

        entry.ChangedByName.Should().Be("Dilara Öz");
        entry.Note.Should().Be("Beklemeye aldım");

        // En son işlem geçmişin başında görünmeli.
        back.StatusHistory.First().Should().Be(entry);
    }

    [Fact]
    public async Task Employee_backward_transition_reaches_the_manager_dashboard()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();

        ActAsEmployee();
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.Assigned, "Beklemede"));

        _h.ActAs(ServiceTestHarness.ManagerId);
        var dashboard = await _h.Dashboard.GetAsync();

        dashboard.RecentTeamUpdates.Should()
            .Contain(u => u.ToStatus == TicketStatus.Assigned && u.Note == "Beklemede");
    }

    [Fact]
    public async Task Employee_cannot_hand_ticket_back_to_unassigned()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var act = () => _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.Unassigned, null));

        (await act.Should().ThrowAsync<DomainRuleException>())
            .Which.Code.Should().Be("EMPLOYEE_TRANSITION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Employee_cannot_archive_ticket()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.Completed, null));

        var act = () => _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.Archived, null));

        (await act.Should().ThrowAsync<DomainRuleException>())
            .Which.Code.Should().Be("EMPLOYEE_TRANSITION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Employee_cannot_change_status_of_someone_elses_ticket()
    {
        var (_, others) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var act = () => _h.Tickets.ChangeStatusAsync(others, new ChangeStatusRequest(TicketStatus.InProgress, null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Employee_cannot_assign_tickets()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var act = () => _h.Tickets.AssignAsync(mine, new AssignTicketRequest(ServiceTestHarness.Employee2Id, null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Employee_detail_offers_only_permitted_next_statuses()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var detail = await _h.Tickets.GetAsync(mine);

        detail.AllowedNextStatuses.Should().Contain(TicketStatus.InProgress);
        detail.AllowedNextStatuses.Should().NotContain(TicketStatus.Unassigned);
        detail.AllowedNextStatuses.Should().NotContain(TicketStatus.Archived);
    }

    [Fact]
    public async Task Manager_only_targets_stay_out_of_reach_after_backward_moves_are_allowed()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, null));
        var detail = await _h.Tickets.GetAsync(mine);

        // Geri alma acildi ama atamayi geri verme ve arsivleme hala mudurde.
        detail.AllowedNextStatuses.Should().Contain(TicketStatus.Assigned);
        detail.AllowedNextStatuses.Should().Contain(TicketStatus.Completed);
        detail.AllowedNextStatuses.Should().NotContain(TicketStatus.Unassigned);
        detail.AllowedNextStatuses.Should().NotContain(TicketStatus.Archived);

        // Mevcut durum menude tekrar onerilmez.
        detail.AllowedNextStatuses.Should().NotContain(detail.Status);
    }

    [Fact]
    public async Task Current_status_is_never_offered_as_a_next_status()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();

        // Assigned durumunda matriste "Assigned -> Assigned" (yeniden atama) girdisi var;
        // durum menusune sizmamali.
        var asManager = await _h.Tickets.GetAsync(mine);
        asManager.Status.Should().Be(TicketStatus.Assigned);
        asManager.AllowedNextStatuses.Should().NotContain(TicketStatus.Assigned);

        ActAsEmployee();
        var asEmployee = await _h.Tickets.GetAsync(mine);
        asEmployee.AllowedNextStatuses.Should().NotContain(TicketStatus.Assigned);
    }

    // -----------------------------------------------------------------------------------
    // Not
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Employee_can_add_note_to_own_ticket()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var note = await _h.Tickets.AddNoteAsync(mine, new AddNoteRequest("Tedarikçiden bilgi bekleniyor."));

        note.AuthorName.Should().Be("Dilara Öz");
    }

    [Fact]
    public async Task Employee_cannot_add_note_to_someone_elses_ticket()
    {
        var (_, others) = await SeedTwoAssignedTicketsAsync();
        ActAsEmployee();

        var act = () => _h.Tickets.AddNoteAsync(others, new AddNoteRequest("Merhaba"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // -----------------------------------------------------------------------------------
    // Müdüre yansıma
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Employee_update_appears_on_manager_dashboard()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();

        ActAsEmployee();
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, "Başladım"));

        _h.ActAs(ServiceTestHarness.ManagerId);
        var dashboard = await _h.Dashboard.GetAsync();

        var update = dashboard.RecentTeamUpdates.Should()
            .ContainSingle(u => u.ExternalTicketNumber == "I260729_000144").Subject;

        update.ChangedByName.Should().Be("Dilara Öz");
        update.ToStatus.Should().Be(TicketStatus.InProgress);
        update.Note.Should().Be("Başladım");
        update.ChangedByUserId.Should().Be(ServiceTestHarness.EmployeeId);
    }

    [Fact]
    public async Task Manager_own_changes_are_not_listed_as_team_updates()
    {
        var (mine, _) = await SeedTwoAssignedTicketsAsync();

        // Müdürün kendi yaptığı geçiş "ekip güncellemesi" sayılmaz.
        await _h.Tickets.ChangeStatusAsync(mine, new ChangeStatusRequest(TicketStatus.InProgress, "Müdür değiştirdi"));

        var dashboard = await _h.Dashboard.GetAsync();

        dashboard.RecentTeamUpdates.Should().BeEmpty();
    }

    [Fact]
    public async Task Manager_still_sees_every_ticket()
    {
        await SeedTwoAssignedTicketsAsync();

        var result = await _h.Tickets.SearchAsync(new TicketQuery());

        result.TotalCount.Should().Be(2);
    }
}
