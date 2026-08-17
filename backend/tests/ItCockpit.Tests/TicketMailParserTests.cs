using FluentAssertions;
using ItCockpit.Application.Parsing;
using ItCockpit.Domain;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Sözleşme: <c>docs/email-parser-contract.md</c>.
/// Referans veri gerçek maildir (<c>docs/ornek-ticket-maili.docx</c>).
/// </summary>
public sealed class TicketMailParserTests
{
    private readonly TicketMailParser _parser = Fixtures.CreateParser();

    // ---------------------------------------------------------------------------------------
    // MVP kabul kriterleri 2-7
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Forwarded_mail_is_accepted_as_ticket_mail()
    {
        var result = _parser.Parse(Fixtures.Load("forwarded-ticket-mail.txt"));

        result.IsTicketMail.Should().BeTrue();
        result.RejectReason.Should().BeNull();
        result.Ticket.Should().NotBeNull();
        result.Ticket!.IsForwarded.Should().BeTrue();
    }

    [Fact]
    public void Extracts_ticket_number_I260729_000144()
    {
        var ticket = ParseForwarded();

        ticket.ExternalTicketNumber.Should().Be("I260729_000144");
        ticket.TicketType.Should().Be(TicketType.Incident);
    }

    [Fact]
    public void Normalizes_requester_name_to_Merve_Turcan()
    {
        ParseForwarded().RequesterName.Should().Be("Merve Turcan");
    }

    [Fact]
    public void Extracts_priority_2()
    {
        var ticket = ParseForwarded();

        ticket.Priority.Should().Be(2);
        ticket.SubjectPriority.Should().Be(2);
    }

    [Fact]
    public void Uses_inner_forward_date_not_outer_envelope_date()
    {
        var ticket = ParseForwarded();

        // 29 Temmuz 2026 11:47 (Europe/Istanbul, UTC+03) => 08:47 UTC
        ticket.OriginalSentAtUtc.Should().Be(new DateTime(2026, 7, 29, 8, 47, 0, DateTimeKind.Utc));

        // Forward tarihi (30 Temmuz 2026 13:33) kullanılmamalı.
        ticket.OriginalSentAtUtc.Should().NotBe(new DateTime(2026, 7, 30, 10, 33, 0, DateTimeKind.Utc));
        ticket.OriginalSentAtUtc.Day.Should().Be(29);
    }

    [Fact]
    public void Converted_to_Istanbul_time_the_original_date_is_29_07_2026_11_47()
    {
        var istanbul = MailDateParser.ResolveTimeZone("Europe/Istanbul");
        var local = TimeZoneInfo.ConvertTimeFromUtc(ParseForwarded().OriginalSentAtUtc, istanbul);

        local.Should().Be(new DateTime(2026, 7, 29, 11, 47, 0));
    }

    // ---------------------------------------------------------------------------------------
    // Diğer alanlar
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Extracts_application_name_from_subject()
    {
        ParseForwarded().ApplicationName.Should().Be("ERP TR");
    }

    [Fact]
    public void Extracts_category_path_from_body()
    {
        ParseForwarded().CategoryPath
            .Should().Be("Incidents/TixHub Categories/Applications & Services - ERP TR");
    }

    [Fact]
    public void Normalizes_NA_external_reference_to_null()
    {
        ParseForwarded().ExternalReference.Should().BeNull();
    }

    [Fact]
    public void Extracts_source_request_id()
    {
        ParseForwarded().SourceRequestId.Should().Be("784090");
    }

    [Fact]
    public void Extracts_body_ticket_number_matching_subject()
    {
        ParseForwarded().BodyTicketNumber.Should().Be("I260729_000144");
    }

    [Fact]
    public void Extracts_original_sender_and_recipients_from_inner_envelope()
    {
        var ticket = ParseForwarded();

        ticket.OriginalSender.Should().Be("ticket@menarini.com");
        ticket.OriginalRecipients.Should().HaveCount(5);
        ticket.OriginalRecipients.Should().Contain("ayilmaz@menarini.com.tr");
        ticket.ForwardedBy.Should().Be("dkeydal@menarini.com.tr");
    }

    [Fact]
    public void Extracts_tixbox_deep_link()
    {
        var url = ParseForwarded().ExternalUrl;

        url.Should().NotBeNull();
        url.Should().StartWith("https://tixcore.menarini.com/autoconnect_mail.php");
        url.Should().Contain("field6=I260729_000144");
    }

    [Fact]
    public void Extracts_description_without_template_boilerplate()
    {
        var description = ParseForwarded().Description;

        description.Should().StartWith("Merhaba,");
        description.Should().Contain("F1 belge türü ile gelen kayıtlar negatiftir");
        description.Should().EndWith("Teşekkürler.");

        description.Should().NotContain("Ticket priority");
        description.Should().NotContain("Service Desk Menarini");
        description.Should().NotContain("Dear colleagues");
        description.Should().NotContain("@REQUEST_ID@");
    }

    [Fact]
    public void Strips_Fwd_prefix_from_subject()
    {
        ParseForwarded().OriginalSubject.Should().StartWith("New Ticket n. I260729_000144");
    }

    [Fact]
    public void Forwarded_reference_mail_produces_no_warnings()
    {
        _parser.Parse(Fixtures.Load("forwarded-ticket-mail.txt")).Warnings.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------
    // Doğrudan (forward edilmemiş) mail
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Direct_mail_uses_received_date_and_is_marked_not_forwarded()
    {
        var mail = Fixtures.Load("direct-ticket-mail.txt");
        var result = _parser.Parse(mail);

        result.IsTicketMail.Should().BeTrue();
        result.Ticket!.IsForwarded.Should().BeFalse();
        result.Ticket.ForwardedBy.Should().BeNull();
        result.Ticket.OriginalSentAtUtc.Should().Be(mail.ReceivedAtUtc);
    }

    [Fact]
    public void Service_request_prefix_S_maps_to_service_request()
    {
        var ticket = _parser.Parse(Fixtures.Load("direct-ticket-mail.txt")).Ticket!;

        ticket.ExternalTicketNumber.Should().Be("S260801_000210");
        ticket.TicketType.Should().Be(TicketType.ServiceRequest);
        ticket.RequesterName.Should().Be("Ali Can Demir");
        ticket.ExternalReference.Should().Be("REQ-8842");
        ticket.Priority.Should().Be(3);
    }

    [Fact]
    public void Hyphenated_external_reference_does_not_leak_into_application_name()
    {
        // "SAP BW - REQ-8842 - Priority: 3" konusunda dış referanstaki tire,
        // uygulama adının içine kaymamalıdır.
        var ticket = _parser.Parse(Fixtures.Load("direct-ticket-mail.txt")).Ticket!;

        ticket.ApplicationName.Should().Be("SAP BW");
        ticket.SubjectPriority.Should().Be(3);
    }

    [Theory]
    [InlineData("New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A - Priority: 2", "ERP TR", "2")]
    [InlineData("New Ticket n. S260801_000210 for Demir, Ali about SAP BW - REQ-8842 - Priority: 3", "SAP BW", "3")]
    [InlineData("New Ticket n. I260901_000001 for Ak, Can about CRM - EU-2026-01-15 - Priority: 1", "CRM", "1")]
    [InlineData("New Ticket n. I260901_000002 for Ak, Can about Web Portal (TR) - N/A - Priority: 4", "Web Portal (TR)", "4")]
    public void Subject_application_segment_is_isolated_from_external_reference(
        string subject, string expectedApp, string expectedPriority)
    {
        var original = Fixtures.Load("direct-ticket-mail.txt");
        var number = subject.Split(' ')[3];

        var mail = original with
        {
            Subject = subject,
            Body = original.Body.Replace("S260801_000210", number)
        };

        var ticket = _parser.Parse(mail).Ticket;

        ticket.Should().NotBeNull();
        ticket!.ApplicationName.Should().Be(expectedApp);
        ticket.SubjectPriority.Should().Be(int.Parse(expectedPriority));
    }

    // ---------------------------------------------------------------------------------------
    // Uyarılar ve reddetme
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Ticket_number_mismatch_still_creates_ticket_and_raises_error_warning()
    {
        var result = _parser.Parse(Fixtures.Load("mismatched-ticket-number-mail.txt"));

        result.IsTicketMail.Should().BeTrue();
        result.Ticket!.ExternalTicketNumber.Should().Be("I260802_000311");  // konudaki kazanır
        result.Ticket.BodyTicketNumber.Should().Be("I260802_000312");

        var warning = result.Warnings.Should()
            .ContainSingle(w => w.Code == ParseWarningCodes.TicketNumberMismatch).Subject;

        warning.Severity.Should().Be(ParseWarningSeverity.Error);
        warning.SubjectValue.Should().Be("I260802_000311");
        warning.BodyValue.Should().Be("I260802_000312");
    }

    [Fact]
    public void Priority_mismatch_prefers_body_value_and_warns()
    {
        var result = _parser.Parse(Fixtures.Load("mismatched-ticket-number-mail.txt"));

        result.Ticket!.SubjectPriority.Should().Be(1);
        result.Ticket.Priority.Should().Be(2);

        result.Warnings.Should().Contain(w =>
            w.Code == ParseWarningCodes.PriorityMismatch && w.Severity == ParseWarningSeverity.Warning);
    }

    [Fact]
    public void Non_ticket_mail_is_rejected_by_sender_filter()
    {
        var result = _parser.Parse(Fixtures.Load("non-ticket-mail.txt"));

        result.IsTicketMail.Should().BeFalse();
        result.Ticket.Should().BeNull();
        result.RejectReason.Should().Be(RejectReasons.SenderNotAllowed);
    }

    [Fact]
    public void Mail_from_allowed_sender_without_subject_pattern_is_rejected()
    {
        var mail = Fixtures.Load("forwarded-ticket-mail.txt") with
        {
            Subject = "Fwd: Sistem bakım bildirimi",
            Body = "Merhaba,\nBakım yapılacaktır.\nService Desk Menarini"
        };

        var result = _parser.Parse(mail);

        result.IsTicketMail.Should().BeFalse();
        result.RejectReason.Should().Be(RejectReasons.SenderNotAllowed);
    }

    [Fact]
    public void Ticket_mail_without_signature_is_rejected()
    {
        var original = Fixtures.Load("direct-ticket-mail.txt");
        var mail = original with { Body = original.Body.Replace("Service Desk Menarini", "Baska Imza") };

        var result = _parser.Parse(mail);

        result.IsTicketMail.Should().BeFalse();
        result.RejectReason.Should().Be(RejectReasons.SignatureMissing);
    }

    [Fact]
    public void Ticket_number_with_unsupported_prefix_is_rejected()
    {
        var original = Fixtures.Load("direct-ticket-mail.txt");
        var mail = original with
        {
            Subject = original.Subject.Replace("S260801_000210", "C260801_000210"),
            Body = original.Body.Replace("S260801_000210", "C260801_000210")
        };

        var result = _parser.Parse(mail);

        result.IsTicketMail.Should().BeFalse();
        result.RejectReason.Should().Be(RejectReasons.TicketNumberFormat);
    }

    [Fact]
    public void Missing_deep_link_raises_info_warning_but_still_parses()
    {
        var original = Fixtures.Load("mismatched-ticket-number-mail.txt");

        var result = _parser.Parse(original);

        result.IsTicketMail.Should().BeTrue();
        result.Ticket!.ExternalUrl.Should().BeNull();
        result.Warnings.Should().Contain(w =>
            w.Code == ParseWarningCodes.ExternalUrlMissing && w.Severity == ParseWarningSeverity.Info);
    }

    // ---------------------------------------------------------------------------------------
    // Gerçek Gmail biçimi: HTML'den düzleştirilmiş gövde, katlanmış başlık satırları
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Gmail'in text/plain sürümü uzun başlık satırlarını ~78 karakterde böler. Gerçek kutuda
    /// konu satırı "... - N/A -" / "Priority: 2" olarak ikiye ayrıldığı için mail reddediliyordu.
    /// </summary>
    [Fact]
    public void Wrapped_subject_header_in_forward_block_is_unfolded()
    {
        var result = _parser.Parse(Fixtures.Load("gmail-html-flattened-mail.txt"));

        result.IsTicketMail.Should().BeTrue(result.RejectReason);
        result.Ticket!.ExternalTicketNumber.Should().Be("I260729_000144");
        result.Ticket.ApplicationName.Should().Be("ERP TR");
        result.Ticket.Priority.Should().Be(2);
        result.Ticket.SubjectPriority.Should().Be(2);
    }

    [Fact]
    public void Wrapped_recipient_header_keeps_all_addresses()
    {
        var ticket = _parser.Parse(Fixtures.Load("gmail-html-flattened-mail.txt")).Ticket!;

        ticket.OriginalRecipients.Should().HaveCount(5);
        ticket.OriginalRecipients.Should().Contain("btufan@menarini.com.tr");
    }

    [Fact]
    public void Flattened_body_still_yields_correct_original_date_and_sender()
    {
        var ticket = _parser.Parse(Fixtures.Load("gmail-html-flattened-mail.txt")).Ticket!;

        ticket.IsForwarded.Should().BeTrue();
        ticket.OriginalSender.Should().Be("ticket@menarini.com");
        ticket.OriginalSentAtUtc.Should().Be(new DateTime(2026, 7, 29, 8, 47, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Flattened_body_fields_are_parsed_despite_blank_line_padding()
    {
        var ticket = _parser.Parse(Fixtures.Load("gmail-html-flattened-mail.txt")).Ticket!;

        ticket.RequesterName.Should().Be("Merve Turcan");
        ticket.CategoryPath.Should().Be("Incidents/TixHub Categories/Applications & Services - ERP TR");
        ticket.ExternalReference.Should().BeNull();
        ticket.SourceRequestId.Should().Be("784090");
        ticket.ExternalUrl.Should().Contain("field6=I260729_000144");
        ticket.Description.Should().Contain("F1 belge türü ile gelen kayıtlar negatiftir");
        ticket.Description.Should().NotContain("Service Desk Menarini");
    }

    // ---------------------------------------------------------------------------------------
    // Zincirli forward — en içteki zarf kullanılmalı
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Chained_forward_uses_innermost_envelope()
    {
        var inner = Fixtures.ReadText("forwarded-ticket-mail.txt");
        var innerBody = inner[(inner.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];

        var outerBody =
            "---------- Forwarded message ---------\n" +
            "From: Ozturk, Nazife <nozturk@menarini.com.tr>\n" +
            "Date: Fri, 31 Jul 2026 at 09:00\n" +
            "Subject: Fwd: Fwd: New Ticket n. I260729_000144 for Turcan, Merve about ERP TR - N/A - Priority: 2\n" +
            "To: Yilmaz, Ahmet <ayilmaz@menarini.com.tr>\n" +
            "\n" +
            innerBody;

        var mail = Fixtures.Load("forwarded-ticket-mail.txt") with { Body = outerBody };
        var result = _parser.Parse(mail);

        result.IsTicketMail.Should().BeTrue();
        result.Ticket!.OriginalSender.Should().Be("ticket@menarini.com");
        result.Ticket.OriginalSentAtUtc.Should().Be(new DateTime(2026, 7, 29, 8, 47, 0, DateTimeKind.Utc));
    }

    private ParsedTicket ParseForwarded() =>
        _parser.Parse(Fixtures.Load("forwarded-ticket-mail.txt")).Ticket!;
}
