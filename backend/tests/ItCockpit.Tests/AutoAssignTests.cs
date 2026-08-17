using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Parsing;
using ItCockpit.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Ticket maili tek kişiye gelmişse sorumlu zaten bellidir; müdürün atamasını beklemez.
/// Gruba gelen mailde atama müdürde kalır — bu ayrım alıcı sayısına bakılarak yapılır, tahminle değil.
/// </summary>
public sealed class AutoAssignTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    /// <summary>Forward zarfındaki <c>To:</c> satırını tek alıcıya indirger.</summary>
    private static RawTicketMail AddressedOnlyTo(string address)
    {
        var mail = Fixtures.Load("forwarded-ticket-mail.txt");

        const string groupLine =
            "To: Yilmaz, Ahmet <ayilmaz@menarini.com.tr>, Oz, Dilara <doz@menarini.com.tr>, " +
            "Secginli, Ayberk <asecginli@menarini.com.tr>, Tufan, Beril <btufan@menarini.com.tr>, " +
            "Keydal, Duygu <dkeydal@menarini.com.tr>";

        return mail with { Body = mail.Body.Replace(groupLine, $"To: <{address}>") };
    }

    [Fact]
    public async Task Ticket_addressed_to_one_known_person_is_auto_assigned()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));   // Dilara Öz

        await _h.Ingestion.RunAsync();

        var ticket = await _h.Db.Tickets.SingleAsync();

        ticket.Status.Should().Be(TicketStatus.Assigned);
        ticket.AssigneeUserId.Should().Be(ServiceTestHarness.EmployeeId);
        ticket.AssignedAtUtc.Should().NotBeNull();
        ticket.AutoAssigned.Should().BeTrue();
    }

    [Fact]
    public async Task Auto_assignment_is_recorded_as_a_system_assignment()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));

        await _h.Ingestion.RunAsync();

        var assignment = await _h.Db.TicketAssignments.SingleAsync();

        assignment.AssignedToUserId.Should().Be(ServiceTestHarness.EmployeeId);
        assignment.AssignedByUserId.Should().BeNull();          // sistem, müdür değil
        assignment.Note.Should().Contain("Kişiye özel");

        var history = await _h.Db.TicketStatusHistory.SingleAsync();
        history.ToStatus.Should().Be(TicketStatus.Assigned);
        history.ChangedByUserId.Should().BeNull();
        history.Note.Should().Contain("Dilara Öz");
    }

    [Fact]
    public async Task Group_ticket_stays_unassigned_for_the_manager()
    {
        // Referans mail 5 kişilik gruba gitmiştir.
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));

        await _h.Ingestion.RunAsync();

        var ticket = await _h.Db.Tickets.SingleAsync();

        ticket.Status.Should().Be(TicketStatus.Unassigned);
        ticket.AssigneeUserId.Should().BeNull();
        ticket.AutoAssigned.Should().BeFalse();
        (await _h.Db.TicketAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Unknown_single_recipient_is_left_unassigned()
    {
        // Sistemde tanimli olmayan bir adres — tahmin yürütülmez.
        _h.GmailSource.Mails.Add(AddressedOnlyTo("bilinmeyen@menarini.com.tr"));

        await _h.Ingestion.RunAsync();

        var ticket = await _h.Db.Tickets.SingleAsync();

        ticket.Status.Should().Be(TicketStatus.Unassigned);
        ticket.AssigneeUserId.Should().BeNull();
        ticket.AutoAssigned.Should().BeFalse();
    }

    [Fact]
    public async Task Inactive_user_does_not_receive_auto_assignment()
    {
        var user = await _h.Db.Users.SingleAsync(u => u.Id == ServiceTestHarness.EmployeeId);
        user.IsActive = false;
        await _h.Db.SaveChangesAsync();

        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));

        await _h.Ingestion.RunAsync();

        (await _h.Db.Tickets.SingleAsync()).AssigneeUserId.Should().BeNull();
    }

    [Fact]
    public async Task Auto_assignment_can_be_switched_off()
    {
        _h.GmailOptions.AutoAssignDirectTickets = false;
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));

        await _h.Ingestion.RunAsync();

        var ticket = await _h.Db.Tickets.SingleAsync();
        ticket.Status.Should().Be(TicketStatus.Unassigned);
        ticket.AutoAssigned.Should().BeFalse();
    }

    // -----------------------------------------------------------------------------------
    // Müdür yine de görür ve müdahale edebilir
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Manager_sees_auto_assigned_ticket_and_can_tell_it_apart()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));
        await _h.Ingestion.RunAsync();

        var list = await _h.Tickets.SearchAsync(new TicketQuery());

        var item = list.Items.Should().ContainSingle().Subject;
        item.AssigneeName.Should().Be("Dilara Öz");
        item.AutoAssigned.Should().BeTrue();

        var detail = await _h.Tickets.GetAsync(item.Id);
        detail.AutoAssigned.Should().BeTrue();
        detail.Assignments.Should().ContainSingle()
            .Which.AssignedByName.Should().Be("Sistem");
    }

    [Fact]
    public async Task Auto_assigned_ticket_appears_in_the_assignees_workload()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));
        await _h.Ingestion.RunAsync();

        var dashboard = await _h.Dashboard.GetAsync();

        dashboard.Metrics.UnassignedTickets.Should().Be(0);
        dashboard.Workload.Single(w => w.UserId == ServiceTestHarness.EmployeeId)
            .OpenTicketCount.Should().Be(1);
    }

    [Fact]
    public async Task Manager_can_reassign_an_auto_assigned_ticket()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));
        await _h.Ingestion.RunAsync();

        var id = await _h.Db.Tickets.Select(t => t.Id).SingleAsync();

        var detail = await _h.Tickets.AssignAsync(
            id, new AssignTicketRequest(ServiceTestHarness.Employee2Id, "Beril daha uygun"));

        detail.AssigneeUserId.Should().Be(ServiceTestHarness.Employee2Id);
        detail.Assignments.Should().HaveCount(2);
        detail.Assignments.Count(a => a.UnassignedAtUtc == null).Should().Be(1);
    }

    [Fact]
    public async Task Auto_assigned_employee_can_work_on_their_ticket_immediately()
    {
        _h.GmailSource.Mails.Add(AddressedOnlyTo("doz@menarini.com.tr"));
        await _h.Ingestion.RunAsync();

        var id = await _h.Db.Tickets.Select(t => t.Id).SingleAsync();

        // Calisan mudur atamasini beklemeden isi ustlenebilir.
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        var detail = await _h.Tickets.ChangeStatusAsync(
            id, new ChangeStatusRequest(TicketStatus.InProgress, "Bakiyorum"));

        detail.Status.Should().Be(TicketStatus.InProgress);
    }
}
