using ItCockpit.Application.Services;
using Microsoft.Extensions.Logging;

namespace ItCockpit.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job. Aralık <c>Gmail:PollIntervalMinutes</c> ile (varsayılan 5 dk)
/// yapılandırılır; işin kendisi zamanlamayı bilmez.
/// </summary>
public sealed class GmailIngestionJob
{
    public const string RecurringJobId = "gmail-ticket-ingestion";

    private readonly TicketIngestionService _ingestion;
    private readonly ILogger<GmailIngestionJob> _logger;

    public GmailIngestionJob(TicketIngestionService ingestion, ILogger<GmailIngestionJob> logger)
    {
        _ingestion = ingestion;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Gmail ingestion job başladı.");
        var result = await _ingestion.RunAsync(ct);
        _logger.LogInformation(
            "Gmail ingestion job bitti: {Created} yeni, {Duplicates} duplicate, {Rejected} reddedildi.",
            result.TicketsCreated, result.DuplicatesSkipped, result.MailsRejected);
    }
}
