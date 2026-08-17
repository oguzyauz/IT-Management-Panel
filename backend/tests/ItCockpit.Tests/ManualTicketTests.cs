using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Panelden elle ticket açma. Maili düşmemiş ama Tixbox'ta var olan kayıtlar için;
/// mailden gelenlerle <b>aynı</b> kurallara tabidir.
/// </summary>
public sealed class ManualTicketTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static CreateTicketRequest Valid(string number = "I260810_000500") => new(
        ExternalTicketNumber: number,
        RequesterName: "Sasmaz, Esra",
        ApplicationName: "ERP TR",
        Priority: 2,
        OriginalSentAtUtc: new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc),
        Description: "Fatura kaydinda hata aliniyor.",
        CategoryPath: "Incidents/TixHub Categories/Applications & Services - ERP TR",
        ExternalReference: null,
        ExternalUrl: null,
        AssigneeUserId: null);

    [Fact]
    public async Task Manager_can_create_a_ticket_by_hand()
    {
        var detail = await _h.Tickets.CreateAsync(Valid());

        detail.ExternalTicketNumber.Should().Be("I260810_000500");
        detail.TicketType.Should().Be(TicketType.Incident);
        detail.Status.Should().Be(TicketStatus.Unassigned);
        detail.CreatedManually.Should().BeTrue();
        detail.MailSources.Should().BeEmpty();          // mail kaynagi yok
    }

    [Fact]
    public async Task Requester_name_is_normalized_like_mail_tickets()
    {
        var detail = await _h.Tickets.CreateAsync(Valid());

        detail.RequesterName.Should().Be("Esra Sasmaz");
    }

    [Fact]
    public async Task Creation_is_recorded_in_status_history_with_the_manager_as_actor()
    {
        var detail = await _h.Tickets.CreateAsync(Valid());

        var entry = detail.StatusHistory.Should().ContainSingle().Subject;
        entry.ToStatus.Should().Be(TicketStatus.Unassigned);
        entry.ChangedByName.Should().Be("Ahmet Yılmaz");
        entry.Note.Should().Be("Panelden elle oluşturuldu");
    }

    [Fact]
    public async Task Can_be_assigned_at_creation_time()
    {
        var detail = await _h.Tickets.CreateAsync(
            Valid() with { AssigneeUserId = ServiceTestHarness.EmployeeId });

        detail.Status.Should().Be(TicketStatus.Assigned);
        detail.AssigneeName.Should().Be("Dilara Öz");
        detail.AutoAssigned.Should().BeFalse();          // mudur atadi, sistem degil
        detail.Assignments.Should().ContainSingle()
            .Which.AssignedByName.Should().Be("Ahmet Yılmaz");
    }

    // ---------------------------------------------------------------------------
    // Dogrulamalar
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("C260810_000500")]   // desteklenmeyen onek
    [InlineData("I26081_000500")]    // eksik hane
    [InlineData("I260810-000500")]   // yanlis ayirici
    [InlineData("rastgele")]
    [InlineData("")]
    public async Task Invalid_ticket_number_is_rejected(string number)
    {
        var act = () => _h.Tickets.CreateAsync(Valid(number));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_TICKET_NUMBER");
    }

    [Fact]
    public async Task Duplicate_ticket_number_is_rejected()
    {
        await _h.Tickets.CreateAsync(Valid());

        var act = () => _h.Tickets.CreateAsync(Valid());

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("DUPLICATE_TICKET");
    }

    [Fact]
    public async Task Cannot_duplicate_a_ticket_that_arrived_by_mail()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        var act = () => _h.Tickets.CreateAsync(Valid("I260729_000144"));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("DUPLICATE_TICKET");
    }

    [Fact]
    public async Task Mail_arriving_later_does_not_create_a_second_record()
    {
        // Once elle acilir, sonra ayni ticketin maili duser.
        await _h.Tickets.CreateAsync(Valid("I260729_000144"));

        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        var result = await _h.Ingestion.RunAsync();

        result.TicketsCreated.Should().Be(0);
        result.DuplicatesSkipped.Should().Be(1);
        (await _h.Db.Tickets.CountAsync()).Should().Be(1);

        // Mail, elle acilan kayda kaynak olarak baglanir.
        (await _h.Db.TicketMailSources.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Priority_outside_one_to_five_is_rejected(int priority)
    {
        var act = () => _h.Tickets.CreateAsync(Valid() with { Priority = priority });

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_PRIORITY");
    }

    [Fact]
    public async Task Empty_requester_or_application_is_rejected()
    {
        var noRequester = () => _h.Tickets.CreateAsync(Valid() with { RequesterName = "  " });
        (await noRequester.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("REQUESTER_REQUIRED");

        var noApp = () => _h.Tickets.CreateAsync(Valid() with { ApplicationName = "" });
        (await noApp.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("APPLICATION_REQUIRED");
    }

    [Fact]
    public async Task Future_opening_date_is_rejected()
    {
        var act = () => _h.Tickets.CreateAsync(
            Valid() with { OriginalSentAtUtc = _h.Clock.UtcNow.AddDays(10) });

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("FUTURE_DATE");
    }

    [Fact]
    public async Task Employee_cannot_create_tickets()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        var act = () => _h.Tickets.CreateAsync(Valid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Manually_created_ticket_behaves_like_any_other()
    {
        var created = await _h.Tickets.CreateAsync(Valid());

        await _h.Tickets.AssignAsync(created.Id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));
        var inProgress = await _h.Tickets.ChangeStatusAsync(
            created.Id, new ChangeStatusRequest(TicketStatus.InProgress, null));

        inProgress.Status.Should().Be(TicketStatus.InProgress);

        var dashboard = await _h.Dashboard.GetAsync();
        dashboard.Metrics.InProgressTickets.Should().Be(1);
    }

    [Fact]
    public async Task Manual_ticket_is_searchable_by_number()
    {
        await _h.Tickets.CreateAsync(Valid());

        var result = await _h.Tickets.SearchAsync(new TicketQuery(Search: "I260810_000500"));

        result.Items.Should().ContainSingle()
            .Which.CreatedManually.Should().BeTrue();
    }
}
