using FluentAssertions;
using ItCockpit.Application.Abstractions;
using ItCockpit.Infrastructure.Gmail;
using Xunit;

namespace ItCockpit.Tests;

public sealed class GmailQueryTests
{
    private static GmailFetchRequest Request(DateTime? since = null) => new(
        MailboxAddress: "ayilmaz@menarini.com.tr",
        LabelName: "Tickets",
        AllowedSenders: ["ticket@menarini.com"],
        SubjectContains: "New Ticket n.",
        SinceHistoryId: null,
        SinceUtc: since,
        MaxResults: 100);

    [Fact]
    public void Query_matches_forwarded_mails_not_only_envelope_sender()
    {
        var query = GmailTicketSource.BuildQuery(Request());

        // Forward edilmiş mailde dış gönderen ileten kişidir; yalnızca from: kullanmak
        // bu mailleri tamamen kaçırır. Adres serbest metin olarak da aranmalıdır.
        query.Should().Contain("from:ticket@menarini.com");
        query.Should().Contain("\"ticket@menarini.com\"");
        query.Should().Contain(" OR ");
    }

    [Fact]
    public void Query_narrows_by_subject_pattern()
    {
        GmailTicketSource.BuildQuery(Request()).Should().Contain("subject:\"New Ticket n.\"");
    }

    [Fact]
    public void Query_includes_date_window_when_provided()
    {
        var query = GmailTicketSource.BuildQuery(Request(new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc)));
        query.Should().Contain("after:2026/07/06");
    }

    [Fact]
    public void Query_omits_date_window_when_not_provided()
    {
        GmailTicketSource.BuildQuery(Request()).Should().NotContain("after:");
    }

    [Fact]
    public void Query_supports_multiple_allowed_senders()
    {
        var request = Request() with { AllowedSenders = ["ticket@menarini.com", "servicedesk@menarini.com"] };
        var query = GmailTicketSource.BuildQuery(request);

        query.Should().Contain("from:ticket@menarini.com");
        query.Should().Contain("from:servicedesk@menarini.com");
        query.Should().Contain("\"servicedesk@menarini.com\"");
    }
}
