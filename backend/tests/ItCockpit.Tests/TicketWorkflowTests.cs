using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Parsing;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>MVP kabul kriterleri 7-10: ingestion, atama, IN_PROGRESS, COMPLETED.</summary>
public sealed class TicketWorkflowTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task<Guid> IngestReferenceTicketAsync()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        return await _h.Db.Tickets
            .Where(t => t.ExternalTicketNumber == "I260729_000144")
            .Select(t => t.Id)
            .SingleAsync();
    }

    [Fact]
    public async Task Ingested_ticket_is_unassigned_with_null_assignee()
    {
        var id = await IngestReferenceTicketAsync();
        var ticket = await _h.Db.Tickets.SingleAsync(t => t.Id == id);

        ticket.Status.Should().Be(TicketStatus.Unassigned);
        ticket.AssigneeUserId.Should().BeNull();
        ticket.AssignedAtUtc.Should().BeNull();
        ticket.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Ingestion_persists_parsed_fields_and_mail_source()
    {
        var id = await IngestReferenceTicketAsync();

        var ticket = await _h.Db.Tickets.Include(t => t.MailSources).SingleAsync(t => t.Id == id);

        ticket.RequesterName.Should().Be("Merve Turcan");
        ticket.Priority.Should().Be(2);
        ticket.TicketType.Should().Be(TicketType.Incident);
        ticket.OriginalSentAtUtc.Should().Be(new DateTime(2026, 7, 29, 8, 47, 0, DateTimeKind.Utc));
        ticket.SourceRequestId.Should().Be("784090");

        var source = ticket.MailSources.Should().ContainSingle().Subject;
        source.IsForwarded.Should().BeTrue();
        source.OriginalSender.Should().Be("ticket@menarini.com");
        source.ForwardedBy.Should().Be("dkeydal@menarini.com.tr");
    }

    [Fact]
    public async Task Ingestion_creates_initial_status_history_with_null_actor()
    {
        var id = await IngestReferenceTicketAsync();

        var history = await _h.Db.TicketStatusHistory.SingleAsync(h => h.TicketId == id);

        history.FromStatus.Should().BeNull();
        history.ToStatus.Should().Be(TicketStatus.Unassigned);
        history.ChangedByUserId.Should().BeNull();
    }

    // -----------------------------------------------------------------------------------
    // Duplicate koruması
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Same_gmail_message_read_twice_creates_only_one_ticket()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));

        await _h.Ingestion.RunAsync();
        var second = await _h.Ingestion.RunAsync();

        (await _h.Db.Tickets.CountAsync()).Should().Be(1);
        (await _h.Db.TicketMailSources.CountAsync()).Should().Be(1);
        second.TicketsCreated.Should().Be(0);
        second.DuplicatesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Second_forward_of_same_ticket_adds_mail_source_without_new_ticket()
    {
        var first = Fixtures.Load("forwarded-ticket-mail.txt");
        await _h.Ingestion.RunAsync();      // boş çalıştırma, state oluşsun

        _h.GmailSource.Mails.Add(first);
        await _h.Ingestion.RunAsync();

        // Aynı ticket, farklı kişi tarafından, farklı Gmail mesaj kimliğiyle iletildi.
        var secondForward = first with
        {
            GmailMessageId = "different-message-id",
            GmailThreadId = "different-thread-id",
            From = "Ozturk, Nazife <nozturk@menarini.com.tr>"
        };

        _h.GmailSource.Mails.Clear();
        _h.GmailSource.Mails.Add(secondForward);
        var result = await _h.Ingestion.RunAsync();

        (await _h.Db.Tickets.CountAsync()).Should().Be(1);
        (await _h.Db.TicketMailSources.CountAsync()).Should().Be(2);
        result.TicketsCreated.Should().Be(0);
        result.DuplicatesSkipped.Should().Be(1);

        var sources = await _h.Db.TicketMailSources.ToListAsync();
        sources.Select(s => s.ForwardedBy).Should()
            .BeEquivalentTo(["dkeydal@menarini.com.tr", "nozturk@menarini.com.tr"]);
    }

    [Fact]
    public async Task Rejected_mail_is_recorded_and_creates_no_ticket()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("non-ticket-mail.txt"));

        var result = await _h.Ingestion.RunAsync();

        result.TicketsCreated.Should().Be(0);
        result.MailsRejected.Should().Be(1);
        (await _h.Db.Tickets.CountAsync()).Should().Be(0);

        var warning = await _h.Db.TicketParseWarnings.SingleAsync();
        warning.TicketId.Should().BeNull();
        warning.Code.Should().Be(RejectReasons.SenderNotAllowed);
    }

    [Fact]
    public async Task Mismatched_ticket_number_creates_ticket_and_persists_error_warning()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("mismatched-ticket-number-mail.txt"));

        await _h.Ingestion.RunAsync();

        var ticket = await _h.Db.Tickets.SingleAsync();
        ticket.ExternalTicketNumber.Should().Be("I260802_000311");

        var warnings = await _h.Db.TicketParseWarnings.Where(w => w.TicketId == ticket.Id).ToListAsync();
        warnings.Should().Contain(w =>
            w.Code == ParseWarningCodes.TicketNumberMismatch && w.Severity == ParseWarningSeverity.Error);
    }

    // -----------------------------------------------------------------------------------
    // Atama ve durum
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Manager_can_assign_ticket_to_employee()
    {
        var id = await IngestReferenceTicketAsync();

        var detail = await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, "Sen bakar mısın?"));

        detail.Status.Should().Be(TicketStatus.Assigned);
        detail.AssigneeUserId.Should().Be(ServiceTestHarness.EmployeeId);
        detail.AssigneeName.Should().Be("Dilara Öz");
        detail.AssignedAtUtc.Should().NotBeNull();
        detail.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task Reassign_closes_previous_assignment_record()
    {
        var id = await IngestReferenceTicketAsync();

        await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));
        var detail = await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.Employee2Id, "Devir"));

        detail.AssigneeUserId.Should().Be(ServiceTestHarness.Employee2Id);
        detail.Assignments.Should().HaveCount(2);
        detail.Assignments.Count(a => a.UnassignedAtUtc == null).Should().Be(1);

        _h.Audit.Entries.Should().Contain(e => e.Action == "TICKET_REASSIGN");
    }

    [Fact]
    public async Task Full_lifecycle_assigned_to_in_progress_to_completed()
    {
        var id = await IngestReferenceTicketAsync();

        await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));

        var inProgress = await _h.Tickets.ChangeStatusAsync(id, new ChangeStatusRequest(TicketStatus.InProgress, "Başladım"));
        inProgress.Status.Should().Be(TicketStatus.InProgress);
        inProgress.CompletedAtUtc.Should().BeNull();

        var completed = await _h.Tickets.ChangeStatusAsync(id, new ChangeStatusRequest(TicketStatus.Completed, "Bitti"));
        completed.Status.Should().Be(TicketStatus.Completed);
        completed.CompletedAtUtc.Should().NotBeNull();

        completed.StatusHistory.Select(h => h.ToStatus).Should()
            .Contain([TicketStatus.Unassigned, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Completed]);
    }

    [Fact]
    public async Task Unassigned_ticket_cannot_be_moved_to_in_progress()
    {
        var id = await IngestReferenceTicketAsync();

        var act = () => _h.Tickets.ChangeStatusAsync(id, new ChangeStatusRequest(TicketStatus.InProgress, null));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Reverting_to_unassigned_clears_assignee()
    {
        var id = await IngestReferenceTicketAsync();
        await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));

        var detail = await _h.Tickets.ChangeStatusAsync(id, new ChangeStatusRequest(TicketStatus.Unassigned, "Geri alındı"));

        detail.Status.Should().Be(TicketStatus.Unassigned);
        detail.AssigneeUserId.Should().BeNull();
        detail.Assignments.Should().OnlyContain(a => a.UnassignedAtUtc != null);
    }

    [Fact]
    public async Task Internal_note_can_be_added_and_bumps_updated_timestamp()
    {
        var id = await IngestReferenceTicketAsync();
        var before = (await _h.Db.Tickets.AsNoTracking().SingleAsync(t => t.Id == id)).UpdatedAtUtc;

        _h.Clock.UtcNow = _h.Clock.UtcNow.AddHours(3);
        var note = await _h.Tickets.AddNoteAsync(id, new AddNoteRequest("Kullanıcıyla görüşüldü."));

        note.Body.Should().Be("Kullanıcıyla görüşüldü.");
        note.AuthorName.Should().Be("Ahmet Yılmaz");

        var after = (await _h.Db.Tickets.AsNoTracking().SingleAsync(t => t.Id == id)).UpdatedAtUtc;
        after.Should().BeAfter(before);
    }

    // -----------------------------------------------------------------------------------
    // Aging — SLA değil
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Aging_uses_configurable_thresholds_not_sla()
    {
        var id = await IngestReferenceTicketAsync();

        // Ticket 29.07.2026'da açıldı; saat 04.08.2026 -> 6 gün açık, 6 gündür güncellenmemiş.
        var detail = await _h.Tickets.GetAsync(id);

        detail.DaysOpen.Should().Be(6);
        detail.Aging.Should().Be(AgingLevel.LongOpen);           // >= 5, < 7

        await _h.Settings.SetAsync(AppSettingKeys.AgingCriticalAfterDays, "6", null);
        (await _h.Tickets.GetAsync(id)).Aging.Should().Be(AgingLevel.LongOpenCritical);
    }

    [Fact]
    public async Task Search_can_filter_unassigned_tickets()
    {
        await IngestReferenceTicketAsync();

        var result = await _h.Tickets.SearchAsync(new TicketQuery(Unassigned: true));

        result.TotalCount.Should().Be(1);
        result.Items.Single().ExternalTicketNumber.Should().Be("I260729_000144");
        result.Items.Single().AssigneeUserId.Should().BeNull();
    }
}
