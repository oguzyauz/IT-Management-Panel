using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Parsing;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

public sealed class WorkScheduleTests : IDisposable
{
    // 4 Ağustos 2026 Salı, 06:00 UTC = 09:00 İstanbul
    private readonly ServiceTestHarness _h = new(new DateTime(2026, 8, 4, 6, 0, 0));

    public void Dispose() => _h.Dispose();

    private static readonly DateOnly NextMonday = new(2026, 8, 10);

    private static SaveMyWeekRequest Week(params WorkMode[] modes) =>
        new(NextMonday, modes.Select((m, i) => new SaveMyWeekDayRequest(NextMonday.AddDays(i), m)).ToList());

    [Fact]
    public void Monday_of_week_is_computed_correctly()
    {
        WorkScheduleService.MondayOf(new DateOnly(2026, 8, 4)).Should().Be(new DateOnly(2026, 8, 3));
        WorkScheduleService.MondayOf(new DateOnly(2026, 8, 9)).Should().Be(new DateOnly(2026, 8, 3)); // Pazar
        WorkScheduleService.MondayOf(new DateOnly(2026, 8, 3)).Should().Be(new DateOnly(2026, 8, 3));
    }

    [Fact]
    public void Default_target_week_is_next_week()
    {
        _h.Schedule.DefaultTargetWeek().Should().Be(NextMonday);
    }

    [Fact]
    public async Task Employee_can_submit_compliant_week_without_violation()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        var result = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice, WorkMode.Office, WorkMode.HomeOffice),
            submit: true);

        result.Status.Should().Be(ScheduleStatus.Submitted);
        result.HasRuleViolation.Should().BeFalse();
        result.Days.Should().HaveCount(5);
        result.Days.Select(d => d.Mode).Should().NotContainNulls();
    }

    [Fact]
    public async Task Rule_violation_is_flagged_but_submission_is_not_blocked()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        var result = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.HomeOffice, WorkMode.HomeOffice, WorkMode.HomeOffice, WorkMode.HomeOffice, WorkMode.Office),
            submit: true);

        result.Status.Should().Be(ScheduleStatus.Submitted);   // engellenmedi
        result.HasRuleViolation.Should().BeTrue();
        result.RuleViolationNote.Should().Contain("Ofis günü sayısı 1");
        result.RuleViolationNote.Should().Contain("Home office günü sayısı 4");
    }

    [Fact]
    public async Task Full_leave_week_does_not_trigger_office_rule()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        var result = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.Leave, WorkMode.Leave, WorkMode.Leave, WorkMode.Leave, WorkMode.Leave),
            submit: true);

        result.HasRuleViolation.Should().BeFalse();
    }

    [Fact]
    public async Task Incomplete_week_is_flagged_as_violation()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        var partial = new SaveMyWeekRequest(NextMonday,
        [
            new SaveMyWeekDayRequest(NextMonday, WorkMode.Office),
            new SaveMyWeekDayRequest(NextMonday.AddDays(1), WorkMode.Office)
        ]);

        var result = await _h.Schedule.SaveMyWeekAsync(partial, submit: false);

        result.HasRuleViolation.Should().BeTrue();
        result.RuleViolationNote.Should().Contain("3 iş günü doldurulmamış");
    }

    [Fact]
    public async Task Submission_after_lock_deadline_is_rejected()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        // Hedef hafta 10 Ağustos; kilit 7 Ağustos Cuma 17:00 yerel. Saati 8 Ağustos'a al.
        _h.Clock.UtcNow = new DateTime(2026, 8, 8, 6, 0, 0, DateTimeKind.Utc);

        var act = () => _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.Office, WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice, WorkMode.HomeOffice),
            submit: true);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("SCHEDULE_LOCKED");
    }

    [Fact]
    public async Task Manager_can_approve_submitted_week()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        var week = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.Office, WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice, WorkMode.HomeOffice),
            submit: true);

        _h.ActAs(ServiceTestHarness.ManagerId);
        var decided = await _h.Schedule.DecideAsync(week.WeekId!.Value, new ScheduleDecisionRequest(ScheduleDecision.Approved, "Uygun"));

        decided.Status.Should().Be(ScheduleStatus.Approved);
        decided.Decisions.Should().ContainSingle().Which.Comment.Should().Be("Uygun");
    }

    [Fact]
    public async Task Draft_week_cannot_be_decided()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        var week = await _h.Schedule.SaveMyWeekAsync(Week(WorkMode.Office), submit: false);

        _h.ActAs(ServiceTestHarness.ManagerId);
        var act = () => _h.Schedule.DecideAsync(week.WeekId!.Value, new ScheduleDecisionRequest(ScheduleDecision.Approved, null));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("NOT_SUBMITTED");
    }

    [Fact]
    public async Task Manager_override_wins_over_employee_value()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        var week = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.HomeOffice, WorkMode.Office, WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice),
            submit: true);

        _h.ActAs(ServiceTestHarness.ManagerId);
        var overridden = await _h.Schedule.OverrideAsync(
            week.WeekId!.Value, new ScheduleOverrideRequest(NextMonday, WorkMode.Office, "Toplantı var"));

        var monday = overridden.Days.Single(d => d.Date == NextMonday);
        monday.Mode.Should().Be(WorkMode.Office);
        monday.IsManagerOverride.Should().BeTrue();
        monday.OverrideNote.Should().Be("Toplantı var");

        // Çalışan tekrar kaydetse bile override korunur.
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        var resaved = await _h.Schedule.SaveMyWeekAsync(
            Week(WorkMode.HomeOffice, WorkMode.Office, WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice),
            submit: false);

        resaved.Days.Single(d => d.Date == NextMonday).Mode.Should().Be(WorkMode.Office);
    }

    [Fact]
    public async Task Public_holiday_is_excluded_from_rule_check()
    {
        // 10 Ağustos 2026 Pazartesi'yi tatil yap.
        _h.Db.WorkCalendar.Add(new WorkCalendarEntry
        {
            Date = NextMonday, Name = "Şirket tatili", Kind = WorkCalendarKind.CompanyHoliday, Year = 2026
        });
        await _h.Db.SaveChangesAsync();

        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);

        // Yalnızca 4 iş günü doldurulur; 3 ofis + 1 home office kuralı sağlar.
        var request = new SaveMyWeekRequest(NextMonday,
        [
            new SaveMyWeekDayRequest(NextMonday.AddDays(1), WorkMode.Office),
            new SaveMyWeekDayRequest(NextMonday.AddDays(2), WorkMode.Office),
            new SaveMyWeekDayRequest(NextMonday.AddDays(3), WorkMode.Office),
            new SaveMyWeekDayRequest(NextMonday.AddDays(4), WorkMode.HomeOffice)
        ]);

        var result = await _h.Schedule.SaveMyWeekAsync(request, submit: true);

        result.HasRuleViolation.Should().BeFalse();
        result.Days.Single(d => d.Date == NextMonday).IsHoliday.Should().BeTrue();
    }

    [Fact]
    public async Task Today_status_reports_who_is_where()
    {
        // Bu haftanın (3 Ağustos) planını doldur; bugün 4 Ağustos Salı.
        var thisMonday = new DateOnly(2026, 8, 3);
        _h.Clock.UtcNow = new DateTime(2026, 7, 30, 6, 0, 0, DateTimeKind.Utc); // kilit öncesi

        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        await _h.Schedule.SaveMyWeekAsync(new SaveMyWeekRequest(thisMonday,
        [
            new SaveMyWeekDayRequest(thisMonday, WorkMode.Office),
            new SaveMyWeekDayRequest(thisMonday.AddDays(1), WorkMode.HomeOffice),
            new SaveMyWeekDayRequest(thisMonday.AddDays(2), WorkMode.Office),
            new SaveMyWeekDayRequest(thisMonday.AddDays(3), WorkMode.Office),
            new SaveMyWeekDayRequest(thisMonday.AddDays(4), WorkMode.Leave)
        ]), submit: true);

        _h.Clock.UtcNow = new DateTime(2026, 8, 4, 6, 0, 0, DateTimeKind.Utc);
        _h.ActAs(ServiceTestHarness.ManagerId);

        var today = await _h.Schedule.GetTodayStatusAsync();

        today.Date.Should().Be(new DateOnly(2026, 8, 4));
        today.Members.Single(m => m.UserId == ServiceTestHarness.EmployeeId).Mode.Should().Be(WorkMode.HomeOffice);
        today.Members.Single(m => m.UserId == ServiceTestHarness.EmployeeId).HasSubmittedWeek.Should().BeTrue();
        today.Members.Single(m => m.UserId == ServiceTestHarness.Employee2Id).Mode.Should().BeNull();
    }

    [Fact]
    public async Task Missing_submissions_are_counted()
    {
        _h.ActAs(ServiceTestHarness.EmployeeId, RoleCodes.Employee);
        await _h.Schedule.SaveMyWeekAsync(Week(WorkMode.Office, WorkMode.Office, WorkMode.Office, WorkMode.HomeOffice, WorkMode.HomeOffice), submit: true);

        // 3 aktif kullanıcı, 1'i gönderdi.
        (await _h.Schedule.CountMissingSubmissionsAsync(NextMonday)).Should().Be(2);
    }
}

public sealed class ReminderTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task<Guid> AssignedTicketAsync()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        var id = await _h.Db.Tickets.Select(t => t.Id).SingleAsync();
        await _h.Tickets.AssignAsync(id, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));
        return id;
    }

    [Fact]
    public async Task Preview_renders_template_placeholders()
    {
        var id = await AssignedTicketAsync();

        var preview = await _h.Reminders.BuildPreviewAsync(
            new ReminderPreviewRequest(ServiceTestHarness.EmployeeId, [id], null, null));

        preview.RecipientName.Should().Be("Dilara Öz");
        preview.RecipientEmail.Should().Be("doz@menarini.com.tr");
        preview.Subject.Should().Be("Açık ticket hatırlatması — 1 ticket");
        preview.Body.Should().Contain("Merhaba Dilara Öz");
        preview.Body.Should().Contain("I260729_000144");
        preview.Body.Should().Contain("Ahmet Yılmaz");
        preview.Body.Should().NotContain("{{");
        preview.Tickets.Should().ContainSingle();
    }

    [Fact]
    public async Task Preview_rejects_tickets_not_assigned_to_recipient()
    {
        var id = await AssignedTicketAsync();

        var act = () => _h.Reminders.BuildPreviewAsync(
            new ReminderPreviewRequest(ServiceTestHarness.Employee2Id, [id], null, null));

        (await act.Should().ThrowAsync<DomainRuleException>())
            .Which.Code.Should().Be("TICKET_NOT_ASSIGNED_TO_RECIPIENT");
    }

    [Fact]
    public async Task Send_without_explicit_confirmation_is_rejected()
    {
        var id = await AssignedTicketAsync();

        var act = () => _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu", "Gövde", null, Confirmed: false));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("NOT_CONFIRMED");
        _h.MailSender.Sent.Should().BeEmpty();
        (await _h.Db.ReminderDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Confirmed_send_uses_edited_text_and_is_logged()
    {
        var id = await AssignedTicketAsync();

        var result = await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null,
            "Elle düzenlenmiş konu", "Elle düzenlenmiş gövde", ["btufan@menarini.com.tr"], Confirmed: true));

        result.Status.Should().Be(ReminderStatus.Sent);
        result.TicketNumbers.Should().BeEquivalentTo(["I260729_000144"]);

        var sent = _h.MailSender.Sent.Should().ContainSingle().Subject;
        sent.Subject.Should().Be("Elle düzenlenmiş konu");   // sunucu preview'ı yeniden üretmez
        sent.Body.Should().Be("Elle düzenlenmiş gövde");
        sent.ToAddress.Should().Be("doz@menarini.com.tr");
        sent.CcAddresses.Should().BeEquivalentTo(["btufan@menarini.com.tr"]);

        var delivery = await _h.Db.ReminderDeliveries.SingleAsync();
        delivery.Status.Should().Be(ReminderStatus.Sent);
        delivery.SentAtUtc.Should().NotBeNull();

        _h.Audit.Entries.Should().Contain(e => e.Action == "REMINDER_SEND");
    }

    [Fact]
    public async Task Failed_send_is_recorded_as_failed_not_lost()
    {
        var id = await AssignedTicketAsync();
        _h.MailSender.ShouldFail = true;

        var result = await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu", "Gövde", null, Confirmed: true));

        result.Status.Should().Be(ReminderStatus.Failed);
        result.ErrorMessage.Should().Be("stub failure");

        var delivery = await _h.Db.ReminderDeliveries.SingleAsync();
        delivery.Status.Should().Be(ReminderStatus.Failed);
        delivery.SentAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task History_reports_ticket_numbers()
    {
        var id = await AssignedTicketAsync();

        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu", "Gövde", null, Confirmed: true));

        var history = await _h.Reminders.GetHistoryAsync();

        history.Should().ContainSingle();
        history[0].RecipientName.Should().Be("Dilara Öz");
        history[0].SentByName.Should().Be("Ahmet Yılmaz");
        history[0].TicketNumbers.Should().BeEquivalentTo(["I260729_000144"]);
    }

    [Fact]
    public async Task History_without_filters_returns_all()
    {
        var id = await AssignedTicketAsync();

        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu 1", "Gövde 1", null, Confirmed: true));
        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu 2", "Gövde 2", null, Confirmed: true));

        var history = await _h.Reminders.GetHistoryAsync();

        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task History_filters_by_date_range()
    {
        var id = await AssignedTicketAsync();

        // Saati 1 Ağustos'a ayarla
        _h.Clock.UtcNow = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Ağustos", "Gövde", null, Confirmed: true));

        // Saati 15 Ağustos'a ayarla
        _h.Clock.UtcNow = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Ağustos Ortası", "Gövde", null, Confirmed: true));

        // Yalnızca 10 Ağustos sonrasını sorgula
        var history = await _h.Reminders.GetHistoryAsync(
            new ReminderHistoryQuery(StartDateUtc: new DateTime(2026, 8, 10)));

        history.Should().ContainSingle()
            .Which.Subject.Should().Be("Ağustos Ortası");
    }

    [Fact]
    public async Task History_filters_by_recipient()
    {
        var id = await AssignedTicketAsync();

        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Konu", "Gövde", null, Confirmed: true));

        // Var olmayan kullanıcıyla filtrele — sonuç boş olmalı
        var history = await _h.Reminders.GetHistoryAsync(
            new ReminderHistoryQuery(RecipientUserId: ServiceTestHarness.Employee2Id));

        history.Should().BeEmpty();

        // Doğru alıcıyla filtrele
        var history2 = await _h.Reminders.GetHistoryAsync(
            new ReminderHistoryQuery(RecipientUserId: ServiceTestHarness.EmployeeId));

        history2.Should().ContainSingle();
    }

    [Fact]
    public async Task History_filters_by_status()
    {
        var id = await AssignedTicketAsync();

        // Başarılı gönderim
        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Başarılı", "Gövde", null, Confirmed: true));

        // Başarısız gönderim
        _h.MailSender.ShouldFail = true;
        await _h.Reminders.SendAsync(new ReminderSendCommand(
            ServiceTestHarness.EmployeeId, [id], null, "Hatalı", "Gövde", null, Confirmed: true));

        var sent = await _h.Reminders.GetHistoryAsync(
            new ReminderHistoryQuery(Status: ReminderStatus.Sent));
        sent.Should().ContainSingle().Which.Subject.Should().Be("Başarılı");

        var failed = await _h.Reminders.GetHistoryAsync(
            new ReminderHistoryQuery(Status: ReminderStatus.Failed));
        failed.Should().ContainSingle().Which.Subject.Should().Be("Hatalı");
    }
}

public sealed class DashboardTests : IDisposable
{
    private readonly ServiceTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Dashboard_reports_open_unassigned_and_aging_counts()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("forwarded-ticket-mail.txt"));
        _h.GmailSource.Mails.Add(Fixtures.Load("direct-ticket-mail.txt"));
        await _h.Ingestion.RunAsync();

        var assignedId = await _h.Db.Tickets
            .Where(t => t.ExternalTicketNumber == "S260801_000210").Select(t => t.Id).SingleAsync();
        await _h.Tickets.AssignAsync(assignedId, new AssignTicketRequest(ServiceTestHarness.EmployeeId, null));
        await _h.Tickets.ChangeStatusAsync(assignedId, new ChangeStatusRequest(TicketStatus.InProgress, null));

        var dashboard = await _h.Dashboard.GetAsync();

        dashboard.Metrics.TotalOpenTickets.Should().Be(2);
        dashboard.Metrics.UnassignedTickets.Should().Be(1);
        dashboard.Metrics.InProgressTickets.Should().Be(1);
        dashboard.UnassignedTickets.Should().ContainSingle()
            .Which.ExternalTicketNumber.Should().Be("I260729_000144");

        dashboard.Workload.Single(w => w.UserId == ServiceTestHarness.EmployeeId)
            .OpenTicketCount.Should().Be(1);

        dashboard.AgingThresholds.Should().Be(new AgingThresholdsDto(2, 5, 7));
    }

    [Fact]
    public async Task Error_level_parse_warnings_surface_on_dashboard()
    {
        _h.GmailSource.Mails.Add(Fixtures.Load("mismatched-ticket-number-mail.txt"));
        await _h.Ingestion.RunAsync();

        var dashboard = await _h.Dashboard.GetAsync();

        dashboard.DataMismatchWarnings.Should()
            .ContainSingle(w => w.Code == ParseWarningCodes.TicketNumberMismatch);

        dashboard.AttentionTickets.Should().ContainSingle().Which.HasParseWarning.Should().BeTrue();
    }
}
