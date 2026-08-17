using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Parsing;
using ItCockpit.Application.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Ticket maili bir gruba gittiği için aynı ticket birden fazla çalışanın kutusunda bulunur.
/// Tüm kutular okunur ama <b>tek</b> ticket kaydı açılır; her kutu ayrı mail kaynağı olarak izlenir.
/// </summary>
public sealed class MultiMailboxTests : IDisposable
{
    private const string ManagerBox = "ayilmaz@menarini.com.tr";
    private const string EmployeeBox = "doz@menarini.com.tr";
    private const string SecondEmployeeBox = "btufan@menarini.com.tr";

    private readonly ServiceTestHarness _h;

    public MultiMailboxTests()
    {
        _h = new ServiceTestHarness();
    }

    public void Dispose() => _h.Dispose();

    /// <summary>Aynı ticket mailinin farklı kutulardaki kopyası — Gmail mesaj kimliği farklıdır.</summary>
    private static RawTicketMail SameTicketSeenIn(string mailboxSuffix)
    {
        var original = Fixtures.Load("forwarded-ticket-mail.txt");
        return original with
        {
            GmailMessageId = $"msg-{mailboxSuffix}",
            GmailThreadId = $"thread-{mailboxSuffix}"
        };
    }

    [Fact]
    public void Effective_mailboxes_falls_back_to_single_address_when_list_empty()
    {
        var options = new GmailIngestionOptions { MailboxAddress = ManagerBox };

        options.EffectiveMailboxes.Should().BeEquivalentTo([ManagerBox]);
    }

    [Fact]
    public void Effective_mailboxes_prefers_the_list_and_ignores_blanks()
    {
        var options = new GmailIngestionOptions
        {
            MailboxAddress = ManagerBox,
            Mailboxes = [EmployeeBox, "   ", SecondEmployeeBox]
        };

        options.EffectiveMailboxes.Should().BeEquivalentTo([EmployeeBox, SecondEmployeeBox]);
    }

    [Fact]
    public async Task Same_ticket_in_three_mailboxes_creates_one_ticket_with_three_sources()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox, SecondEmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];
        _h.GmailSource.MailsPerMailbox[SecondEmployeeBox] = [SameTicketSeenIn("emp2")];

        var result = await _h.Ingestion.RunAsync();

        result.MessagesSeen.Should().Be(3);
        result.TicketsCreated.Should().Be(1);
        result.DuplicatesSkipped.Should().Be(2);

        (await _h.Db.Tickets.CountAsync()).Should().Be(1);

        var sources = await _h.Db.TicketMailSources.ToListAsync();
        sources.Should().HaveCount(3);
        sources.Select(s => s.SourceMailbox).Should()
            .BeEquivalentTo([ManagerBox, EmployeeBox, SecondEmployeeBox]);
    }

    // --- Okuma penceresi ------------------------------------------------------------------------

    [Fact]
    public async Task Failed_read_does_not_move_the_sync_window_forward()
    {
        _h.UseMailboxes(EmployeeBox);
        _h.GmailSource.FailingMailboxes.Add(EmployeeBox);

        await _h.Ingestion.RunAsync();

        var state = await _h.Db.GmailSyncStates.SingleAsync(s => s.MailboxAddress == EmployeeBox);

        state.LastSyncStatus.Should().Be("FAILED");

        // Damga ilerlerse, hata giderildikten sonra kutudaki eski mailler kalici olarak atlanir.
        state.LastSyncCompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Mails_older_than_a_failed_attempt_are_still_read_once_it_is_fixed()
    {
        _h.UseMailboxes(EmployeeBox);

        // 1) Kutu henuz yetkilendirilmemis: okuma hata veriyor.
        _h.GmailSource.FailingMailboxes.Add(EmployeeBox);
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();
        (await _h.Db.Tickets.CountAsync()).Should().Be(0);

        // 2) Kullanici yetkilendirmeyi tamamliyor ve tekrar okutuyor.
        _h.GmailSource.FailingMailboxes.Clear();
        _h.GmailSource.Requests.Clear();
        _h.Clock.Advance(TimeSpan.FromMinutes(5));

        var result = await _h.Ingestion.RunAsync();

        // Pencere ilk okuma gibi geriye bakmali, "son senkrondan beri" olmamali.
        var request = _h.GmailSource.Requests.Should().ContainSingle().Subject;
        request.SinceUtc.Should().BeBefore(_h.Clock.UtcNow.AddDays(-1));

        result.TicketsCreated.Should().Be(1);
    }

    [Fact]
    public async Task Successful_read_does_move_the_sync_window_forward()
    {
        _h.UseMailboxes(EmployeeBox);
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();

        var state = await _h.Db.GmailSyncStates.SingleAsync(s => s.MailboxAddress == EmployeeBox);
        state.LastSyncCompletedAtUtc.Should().Be(_h.Clock.UtcNow);
    }

    [Fact]
    public async Task Rescan_restarts_the_read_window_without_removing_the_mailbox()
    {
        _h.UseMailboxes(EmployeeBox);
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();
        (await _h.Db.GmailSyncStates.AnyAsync(s => s.MailboxAddress == EmployeeBox)).Should().BeTrue();

        await _h.Mailboxes.ResetSyncStateAsync(EmployeeBox);

        (await _h.Db.GmailSyncStates.AnyAsync(s => s.MailboxAddress == EmployeeBox)).Should().BeFalse();

        // Kutu listede kalir; yeniden yetkilendirme gerekmez.
        (await _h.Mailboxes.GetAsync()).Should().Contain(EmployeeBox);
    }

    [Fact]
    public async Task Rescan_reads_the_full_window_again_without_duplicating_tickets()
    {
        _h.UseMailboxes(EmployeeBox);
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();
        (await _h.Db.Tickets.CountAsync()).Should().Be(1);

        await _h.Mailboxes.ResetSyncStateAsync(EmployeeBox);
        _h.GmailSource.Requests.Clear();
        _h.Clock.Advance(TimeSpan.FromMinutes(10));

        var result = await _h.Ingestion.RunAsync();

        var request = _h.GmailSource.Requests.Should().ContainSingle().Subject;
        request.SinceUtc.Should().BeBefore(_h.Clock.UtcNow.AddDays(-1));

        // Ayni mail tekrar okundu ama ikinci ticket acilmadi.
        result.DuplicatesSkipped.Should().Be(1);
        (await _h.Db.Tickets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Rescanning_an_unknown_mailbox_is_rejected()
    {
        _h.UseMailboxes(ManagerBox);

        var act = () => _h.Mailboxes.ResetSyncStateAsync("bilinmeyen@menarini.com.tr");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Re_adding_a_mailbox_restarts_its_read_window()
    {
        _h.UseMailboxes(ManagerBox);
        await _h.Mailboxes.AddAsync(EmployeeBox);

        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];
        await _h.Ingestion.RunAsync();

        (await _h.Db.GmailSyncStates.AnyAsync(s => s.MailboxAddress == EmployeeBox)).Should().BeTrue();

        await _h.Mailboxes.RemoveAsync(EmployeeBox);

        // Kutunun durumu da silinmeli: kullanicinin elindeki "bastan tara" yolu budur.
        (await _h.Db.GmailSyncStates.AnyAsync(s => s.MailboxAddress == EmployeeBox)).Should().BeFalse();
    }

    // --- Kutu listesinin yönetimi ---------------------------------------------------------------

    [Fact]
    public void Fresh_install_with_no_configured_mailbox_reads_nothing()
    {
        // Buraya bir varsayilan adres dusserse, yetkilendirilmemis oldugu icin ilk okumada
        // kalici FAILED kaydi olusur ve kullanici daha hicbir sey yapmadan hata gorur.
        var options = new GmailIngestionOptions();

        options.EffectiveMailboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task Ingestion_is_a_no_op_when_no_mailbox_is_configured()
    {
        // Kutusuz yeni kurulum: paketten cikan appsettings'te de Mailboxes bostur.
        _h.GmailOptions.MailboxAddress = string.Empty;
        _h.UseMailboxes();

        var result = await _h.Ingestion.RunAsync();

        result.MessagesSeen.Should().Be(0);
        result.PerMailbox.Should().BeEmpty();
        result.RejectReasons.Should().BeEmpty();

        // En onemlisi: hicbir kutu icin hata durumu yazilmamali.
        (await _h.Db.GmailSyncStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Mailbox_list_falls_back_to_configuration_when_nothing_was_saved()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        // Mevcut kurulumlar appsettings.json ile çalışmaya devam etmeli.
        (await _h.Mailboxes.GetAsync()).Should().BeEquivalentTo([ManagerBox, EmployeeBox]);
    }

    [Fact]
    public async Task Added_mailbox_overrides_the_configured_list()
    {
        _h.UseMailboxes(ManagerBox);

        await _h.Mailboxes.AddAsync(EmployeeBox);

        (await _h.Mailboxes.GetAsync()).Should().BeEquivalentTo([ManagerBox, EmployeeBox]);
    }

    [Fact]
    public async Task Mailbox_address_is_normalized_so_case_does_not_create_duplicates()
    {
        _h.UseMailboxes(ManagerBox);

        await _h.Mailboxes.AddAsync("  DOZ@Menarini.Com.TR  ");

        (await _h.Mailboxes.GetAsync()).Should().Contain(EmployeeBox);

        var act = () => _h.Mailboxes.AddAsync(EmployeeBox);
        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("DUPLICATE_MAILBOX");
    }

    [Fact]
    public async Task Mailbox_without_an_at_sign_is_rejected()
    {
        var act = () => _h.Mailboxes.AddAsync("bu bir adres degil");

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_MAILBOX");
    }

    [Fact]
    public async Task Removed_mailbox_is_no_longer_read_but_its_tickets_remain()
    {
        _h.UseMailboxes(ManagerBox);
        await _h.Mailboxes.AddAsync(EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();
        (await _h.Db.Tickets.CountAsync()).Should().Be(1);

        await _h.Mailboxes.RemoveAsync(EmployeeBox);

        (await _h.Mailboxes.GetAsync()).Should().BeEquivalentTo([ManagerBox]);

        // Kutu listeden çıktı diye geçmiş kayıtlar silinmemeli.
        (await _h.Db.Tickets.CountAsync()).Should().Be(1);
        (await _h.Db.TicketMailSources.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Ingestion_reads_the_mailboxes_saved_at_runtime()
    {
        _h.UseMailboxes(ManagerBox);
        await _h.Mailboxes.AddAsync(EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        var result = await _h.Ingestion.RunAsync();

        result.PerMailbox.Select(m => m.MailboxAddress)
            .Should().BeEquivalentTo([ManagerBox, EmployeeBox]);
    }

    [Fact]
    public async Task Ticket_list_reports_every_mailbox_the_mail_was_read_from()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();

        var page = await _h.Tickets.SearchAsync(new TicketQuery());

        page.Items.Should().ContainSingle().Which
            .SourceMailboxes.Should().BeEquivalentTo([ManagerBox, EmployeeBox]);
    }

    [Fact]
    public async Task Tickets_can_be_filtered_by_the_mailbox_they_were_read_from()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] =
        [
            Fixtures.Load("direct-ticket-mail.txt") with
            {
                GmailMessageId = "msg-emp-only",
                GmailThreadId = "thread-emp-only"
            }
        ];

        await _h.Ingestion.RunAsync();

        var onlyEmployeeBox = await _h.Tickets.SearchAsync(new TicketQuery(Search: "doz@"));

        onlyEmployeeBox.Items.Should().ContainSingle().Which
            .SourceMailboxes.Should().BeEquivalentTo([EmployeeBox]);
    }

    [Fact]
    public async Task Per_mailbox_breakdown_is_reported()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] =
        [
            SameTicketSeenIn("emp1"),
            Fixtures.Load("direct-ticket-mail.txt")
        ];

        var result = await _h.Ingestion.RunAsync();

        result.PerMailbox.Should().HaveCount(2);

        var manager = result.PerMailbox.Single(m => m.MailboxAddress == ManagerBox);
        manager.MessagesSeen.Should().Be(1);
        manager.TicketsCreated.Should().Be(1);

        var employee = result.PerMailbox.Single(m => m.MailboxAddress == EmployeeBox);
        employee.MessagesSeen.Should().Be(2);
        employee.TicketsCreated.Should().Be(1);      // yalnizca S260801_000210 yeni
        employee.DuplicatesSkipped.Should().Be(1);   // I260729_000144 zaten var
    }

    [Fact]
    public async Task Each_mailbox_gets_its_own_sync_state()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [];

        await _h.Ingestion.RunAsync();

        var states = await _h.Db.GmailSyncStates.ToListAsync();

        states.Should().HaveCount(2);
        states.Single(s => s.MailboxAddress == ManagerBox).TicketsCreated.Should().Be(1);
        states.Single(s => s.MailboxAddress == EmployeeBox).MessagesSeen.Should().Be(0);
        states.Should().OnlyContain(s => s.LastSyncStatus == "SUCCESS");
    }

    [Fact]
    public async Task Failing_mailbox_does_not_stop_the_others()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox, SecondEmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.FailingMailboxes.Add(EmployeeBox);   // ör. henuz yetkilendirilmemis
        _h.GmailSource.MailsPerMailbox[SecondEmployeeBox] = [Fixtures.Load("direct-ticket-mail.txt")];

        var result = await _h.Ingestion.RunAsync();

        // Hatali kutu raporlanir ama digerleri islenmeye devam eder.
        result.TicketsCreated.Should().Be(2);

        var failed = result.PerMailbox.Single(m => m.MailboxAddress == EmployeeBox);
        failed.Error.Should().NotBeNull();
        failed.MessagesSeen.Should().Be(0);

        var state = await _h.Db.GmailSyncStates.SingleAsync(s => s.MailboxAddress == EmployeeBox);
        state.LastSyncStatus.Should().Be("FAILED");
        state.LastError.Should().NotBeNull();

        (await _h.Db.Tickets.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Rereading_all_mailboxes_creates_nothing_new()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [SameTicketSeenIn("emp1")];

        await _h.Ingestion.RunAsync();
        var second = await _h.Ingestion.RunAsync();

        second.TicketsCreated.Should().Be(0);
        second.DuplicatesSkipped.Should().Be(2);

        (await _h.Db.Tickets.CountAsync()).Should().Be(1);
        (await _h.Db.TicketMailSources.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Preview_reports_which_mailbox_each_mail_came_from()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.MailsPerMailbox[EmployeeBox] = [Fixtures.Load("direct-ticket-mail.txt")];

        var preview = await _h.Ingestion.PreviewAsync();

        preview.Items.Should().HaveCount(2);
        preview.Items.Should().Contain(i => i.SourceMailbox == ManagerBox && i.ExternalTicketNumber == "I260729_000144");
        preview.Items.Should().Contain(i => i.SourceMailbox == EmployeeBox && i.ExternalTicketNumber == "S260801_000210");

        // Kuru calistirma hicbir sey kaydetmez.
        (await _h.Db.Tickets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Preview_reports_mailbox_error_without_failing()
    {
        _h.UseMailboxes(ManagerBox, EmployeeBox);

        _h.GmailSource.MailsPerMailbox[ManagerBox] = [SameTicketSeenIn("mgr")];
        _h.GmailSource.FailingMailboxes.Add(EmployeeBox);

        var preview = await _h.Ingestion.PreviewAsync();

        preview.Items.Should().Contain(i =>
            i.SourceMailbox == EmployeeBox && i.RejectReason!.StartsWith("MAILBOX_ERROR"));

        preview.Items.Should().Contain(i => i.SourceMailbox == ManagerBox && i.IsTicketMail);
    }
}
