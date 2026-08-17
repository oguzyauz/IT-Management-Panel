using System.Text.Json;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Parsing;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItCockpit.Application.Services;

public sealed class GmailIngestionOptions
{
    public const string SectionName = "Gmail";

    public string Provider { get; set; } = "Mock";           // Mock | Google

    /// <summary>
    /// Tek kutu kullanımı için geriye dönük ayar. <see cref="Mailboxes"/> doluysa yok sayılır.
    /// <b>Varsayılanı boştur:</b> yeni bir kurulumda hiçbir kutu yapılandırılmamışken buraya bir
    /// adres düşerse, o kutu yetkilendirilmediği için ilk okumada kalıcı hata kaydı oluşur ve
    /// kullanıcı daha hiçbir şey yapmadan hatayla karşılaşır.
    /// </summary>
    public string MailboxAddress { get; set; } = string.Empty;

    /// <summary>
    /// Okunacak posta kutuları. Ticket maili bir gruba gittiği için aynı ticket birden fazla
    /// kutuda bulunabilir; duplicate koruması tek kayıt açılmasını garanti eder.
    /// Her kutu <b>ayrı ayrı</b> OAuth onayı gerektirir.
    /// </summary>
    public string[] Mailboxes { get; set; } = [];

    /// <summary>
    /// Yapılandırmadan gerçekten okunacak kutu listesi. Hiçbir kutu tanımlanmamışsa
    /// <b>boş</b> döner; okuma yapılmaz ve hata da üretilmez.
    /// </summary>
    public IReadOnlyList<string> EffectiveMailboxes =>
        Mailboxes.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray() is { Length: > 0 } list
            ? list
            : string.IsNullOrWhiteSpace(MailboxAddress)
                ? []
                : [MailboxAddress];
    public string? TicketLabel { get; set; } = "Tickets";
    public string SubjectContains { get; set; } = "New Ticket n.";
    public int PollIntervalMinutes { get; set; } = 5;
    public int MaxResultsPerRun { get; set; } = 100;

    /// <summary>
    /// İlk çalıştırmada kaç gün geriye bakılacağı. Kutunun tamamının taranmasını önler;
    /// sonraki çalıştırmalarda son senkron tarihi esas alınır.
    /// </summary>
    public int InitialLookbackDays { get; set; } = 30;

    /// <summary>
    /// Ticket maili tek bir kişiye gönderilmişse (gruba değil) ticket doğrudan o kişiye atanır.
    /// Kapalıysa her ticket <c>UNASSIGNED</c> düşer ve atamayı müdür yapar.
    /// </summary>
    public bool AutoAssignDirectTickets { get; set; } = true;
    public string CredentialsPath { get; set; } = "credentials.json";
    public string TokenStorePath { get; set; } = "token-store";
}

/// <summary>
/// Gmail'den gelen mailleri ayrıştırıp veritabanına yazar.
/// Duplicate kontrolü 4 aşamalıdır (bkz. docs/email-parser-contract.md §8);
/// aynı <c>ExternalTicketNumber</c> için ikinci ticket <b>asla</b> oluşturulmaz.
/// </summary>
public sealed class TicketIngestionService
{
    private readonly IAppDbContext _db;
    private readonly IGmailTicketSource _source;
    private readonly TicketMailParser _parser;
    private readonly IClock _clock;
    private readonly GmailIngestionOptions _options;
    private readonly TicketMailParserOptions _parserOptions;
    private readonly MailboxRegistry _mailboxes;
    private readonly ILogger<TicketIngestionService> _logger;

    public TicketIngestionService(
        IAppDbContext db,
        IGmailTicketSource source,
        TicketMailParser parser,
        IClock clock,
        IOptions<GmailIngestionOptions> options,
        IOptions<TicketMailParserOptions> parserOptions,
        MailboxRegistry mailboxes,
        ILogger<TicketIngestionService> logger)
    {
        _db = db;
        _source = source;
        _parser = parser;
        _clock = clock;
        _options = options.Value;
        _parserOptions = parserOptions.Value;
        _mailboxes = mailboxes;
        _logger = logger;
    }

    /// <summary>
    /// Yapılandırılmış tüm posta kutularını sırayla okur. Bir kutunun hatası (ör. yetkilendirilmemiş)
    /// diğerlerini durdurmaz; her kutunun durumu kendi <see cref="GmailSyncState"/> kaydında tutulur.
    /// </summary>
    public async Task<IngestionRunResultDto> RunAsync(CancellationToken ct = default)
    {
        var startedAt = _clock.UtcNow;

        var created = new List<string>();
        var rejects = new List<string>();
        var perMailbox = new List<MailboxIngestionResultDto>();
        var duplicates = 0;
        var warningsRaised = 0;
        var seen = 0;

        foreach (var mailbox in await _mailboxes.GetAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var result = await RunForMailboxAsync(mailbox, startedAt, ct);

            seen += result.MessagesSeen;
            duplicates += result.DuplicatesSkipped;
            warningsRaised += result.WarningsRaised;
            created.AddRange(result.CreatedTicketNumbers);
            rejects.AddRange(result.RejectReasons);

            perMailbox.Add(new MailboxIngestionResultDto(
                mailbox, result.MessagesSeen, result.CreatedTicketNumbers.Count,
                result.DuplicatesSkipped, result.RejectReasons.Count, result.Error));
        }

        var completedAt = _clock.UtcNow;

        _logger.LogInformation(
            "Ingestion tamamlandı ({Mailboxes} kutu): {Seen} mail, {Created} yeni ticket, {Duplicates} duplicate, {Rejected} reddedildi",
            perMailbox.Count, seen, created.Count, duplicates, rejects.Count);

        return new IngestionRunResultDto(
            _source.ProviderName, seen, created.Count, duplicates, rejects.Count, warningsRaised,
            created, rejects, perMailbox, startedAt, completedAt);
    }

    private sealed record MailboxRunResult(
        int MessagesSeen,
        List<string> CreatedTicketNumbers,
        List<string> RejectReasons,
        int DuplicatesSkipped,
        int WarningsRaised,
        string? Error);

    private async Task<MailboxRunResult> RunForMailboxAsync(
        string mailbox, DateTime startedAt, CancellationToken ct)
    {
        var state = await _db.GmailSyncStates.FirstOrDefaultAsync(s => s.MailboxAddress == mailbox, ct);

        if (state is null)
        {
            state = new GmailSyncState { MailboxAddress = mailbox };
            _db.GmailSyncStates.Add(state);
        }

        state.LastSyncStartedAtUtc = startedAt;

        var created = new List<string>();
        var rejects = new List<string>();
        var duplicates = 0;
        var warningsRaised = 0;
        var seen = 0;
        string? error = null;

        try
        {
            // İlk çalıştırmada geriye dönük pencere sınırlıdır; sonrasında son başarılı senkrondan
            // bir gün önceye bakılır (Gmail 'after:' gün hassasiyetinde olduğu için güvenli örtüşme).
            var sinceUtc = state.LastSyncCompletedAtUtc is { } lastSync
                ? lastSync.AddDays(-1)
                : startedAt.AddDays(-Math.Max(1, _options.InitialLookbackDays));

            var request = new GmailFetchRequest(
                mailbox,
                _options.TicketLabel,
                _parserOptions.AllowedSenders,
                _options.SubjectContains,
                state.LastHistoryId,
                sinceUtc,
                _options.MaxResultsPerRun);

            var mails = await _source.FetchAsync(request, ct);
            seen = mails.Count;

            foreach (var mail in mails)
            {
                var outcome = await IngestOneAsync(mail, mailbox, ct);
                switch (outcome.Kind)
                {
                    case IngestOutcomeKind.Created:
                        created.Add(outcome.TicketNumber!);
                        break;
                    case IngestOutcomeKind.Duplicate:
                        duplicates++;
                        break;
                    case IngestOutcomeKind.Rejected:
                        rejects.Add(outcome.RejectReason!);
                        break;
                }

                warningsRaised += outcome.WarningCount;
            }

            state.LastSyncStatus = "SUCCESS";
            state.LastError = null;

            // Okuma penceresi YALNIZCA başarılı okumadan sonra ilerletilir.
            //
            // Bu satır try/catch dışındayken, henüz yetkilendirilmemiş bir kutudaki başarısız
            // deneme de damgayı bugüne taşıyordu. Kullanıcı yetkilendirmeyi tamamladığında
            // sonraki okuma "son senkrondan beri" diye bugünden sorgu atıyor ve kutudaki eski
            // ticket mailleri kalıcı olarak atlanıyordu — hata da vermeden.
            state.LastSyncCompletedAtUtc = _clock.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bir kutunun hatası diğer kutuları durdurmaz — ör. henüz yetkilendirilmemiş bir çalışan.
            // Damga güncellenmez: hata giderildiğinde aynı pencere baştan taranmalı.
            error = ex.Message;
            state.LastSyncStatus = "FAILED";
            state.LastError = ex.Message;
            _logger.LogError(ex, "Gmail ingestion başarısız oldu (kutu: {Mailbox})", mailbox);
        }

        state.MessagesSeen += seen;
        state.TicketsCreated += created.Count;
        state.DuplicatesSkipped += duplicates;
        state.MailsRejected += rejects.Count;

        await _db.SaveChangesAsync(ct);

        return new MailboxRunResult(seen, created, rejects, duplicates, warningsRaised, error);
    }

    /// <summary>
    /// Mailleri okur ve ayrıştırır ama <b>hiçbir şey kaydetmez</b>. Gerçek bir kutuya bağlanırken
    /// parser'ın ne gördüğünü teşhis etmek için kullanılır.
    /// </summary>
    public async Task<IngestionPreviewDto> PreviewAsync(int? maxResults = null, CancellationToken ct = default)
    {
        var items = new List<IngestionPreviewItemDto>();

        foreach (var mailbox in await _mailboxes.GetAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var state = await _db.GmailSyncStates.AsNoTracking()
                .FirstOrDefaultAsync(s => s.MailboxAddress == mailbox, ct);

            var sinceUtc = state?.LastSyncCompletedAtUtc is { } lastSync
                ? lastSync.AddDays(-1)
                : _clock.UtcNow.AddDays(-Math.Max(1, _options.InitialLookbackDays));

            var request = new GmailFetchRequest(
                mailbox,
                _options.TicketLabel,
                _parserOptions.AllowedSenders,
                _options.SubjectContains,
                state?.LastHistoryId,
                sinceUtc,
                maxResults ?? _options.MaxResultsPerRun);

            IReadOnlyList<RawTicketMail> mails;
            try
            {
                mails = await _source.FetchAsync(request, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Kuru çalıştırmada bir kutunun hatası diğerlerini engellemez; hata satır olarak raporlanır.
                items.Add(IngestionPreviewItemDto.ForMailboxError(mailbox, ex.Message));
                continue;
            }

            foreach (var mail in mails)
            {
                var body = (mail.Body ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
                var envelope = ForwardEnvelopeExtractor.TryExtract(body);
                var result = _parser.Parse(mail);
                var parsed = result.Ticket;

                items.Add(new IngestionPreviewItemDto(
                    mailbox,
                    mail.GmailMessageId,
                    mail.Subject,
                    mail.From,
                    mail.ReceivedAtUtc,
                    body.Length,
                    envelope is not null,
                    envelope?.From,
                    envelope?.RawDate,
                    envelope?.Subject,
                    result.IsTicketMail,
                    result.RejectReason,
                    parsed?.ExternalTicketNumber,
                    parsed?.RequesterName,
                    parsed?.ApplicationName,
                    parsed?.Priority,
                    parsed?.OriginalSentAtUtc,
                    result.Warnings.Select(w => $"{w.Severity}:{w.Code}").ToList()));
            }
        }

        return new IngestionPreviewDto(_source.ProviderName, items.Count, items);
    }

    private enum IngestOutcomeKind { Created, Duplicate, Rejected }

    private readonly record struct IngestOutcome(
        IngestOutcomeKind Kind, string? TicketNumber, string? RejectReason, int WarningCount);

    private async Task<IngestOutcome> IngestOneAsync(RawTicketMail mail, string sourceMailbox, CancellationToken ct)
    {
        // Duplicate anahtarı #1 — aynı Gmail mesajı daha önce işlendiyse hiçbir şey yapma.
        var alreadySeen = await _db.TicketMailSources
            .AnyAsync(s => s.GmailMessageId == mail.GmailMessageId, ct);

        if (alreadySeen)
            return new IngestOutcome(IngestOutcomeKind.Duplicate, null, null, 0);

        var result = _parser.Parse(mail);

        if (!result.IsTicketMail || result.Ticket is null)
        {
            _logger.LogInformation("Mail reddedildi ({Reason}): {MessageId}", result.RejectReason, mail.GmailMessageId);

            _db.TicketParseWarnings.Add(new TicketParseWarning
            {
                TicketId = null,
                GmailMessageId = mail.GmailMessageId,
                Code = result.RejectReason ?? "REJECTED",
                Severity = ParseWarningSeverity.Info,
                Message = $"Mail ticket maili olarak kabul edilmedi: {result.RejectReason}",
                SubjectValue = Truncate(mail.Subject, 500),
                CreatedAtUtc = _clock.UtcNow
            });

            return new IngestOutcome(IngestOutcomeKind.Rejected, null, result.RejectReason, 0);
        }

        var parsed = result.Ticket;

        // Duplicate anahtarları #2, #3, #4
        var existing = await FindExistingTicketAsync(parsed, ct);

        if (existing is not null)
        {
            AddMailSource(existing.Id, mail, parsed, sourceMailbox);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Var olan ticket {Number} için yeni mail kaynağı eklendi ({MessageId})",
                existing.ExternalTicketNumber, mail.GmailMessageId);

            return new IngestOutcome(IngestOutcomeKind.Duplicate, existing.ExternalTicketNumber, null, 0);
        }

        var now = _clock.UtcNow;

        // Kişiye özel gelen ticket'ta sorumlu zaten bellidir; müdürün atamasını beklemesin.
        var directAssignee = await ResolveDirectAssigneeAsync(parsed, ct);

        var ticket = new Ticket
        {
            ExternalTicketNumber = parsed.ExternalTicketNumber,
            TicketType = parsed.TicketType,
            RequesterName = parsed.RequesterName,
            ApplicationName = parsed.ApplicationName,
            Description = parsed.Description,
            Priority = parsed.Priority,
            CategoryPath = parsed.CategoryPath,
            ExternalReference = parsed.ExternalReference,
            SourceRequestId = parsed.SourceRequestId,
            OriginalSentAtUtc = parsed.OriginalSentAtUtc,
            ExternalUrl = parsed.ExternalUrl,

            // Gruba gelen mailde atama yapılmaz; kişiye özel mailde sorumlu zaten bellidir.
            Status = directAssignee is null ? TicketStatus.Unassigned : TicketStatus.Assigned,
            AssigneeUserId = directAssignee?.Id,
            AssignedAtUtc = directAssignee is null ? null : now,
            AutoAssigned = directAssignee is not null,

            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Tickets.Add(ticket);
        AddMailSource(ticket.Id, mail, parsed, sourceMailbox);

        _db.TicketStatusHistory.Add(new TicketStatusHistory
        {
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = ticket.Status,
            ChangedByUserId = null, // sistem
            ChangedAtUtc = now,
            Note = directAssignee is null
                ? "Mail ile otomatik oluşturuldu"
                : $"Mail ile otomatik oluşturuldu ve {directAssignee.DisplayName} kişisine atandı (kişiye özel mail)"
        });

        if (directAssignee is not null)
        {
            _db.TicketAssignments.Add(new TicketAssignment
            {
                TicketId = ticket.Id,
                AssignedToUserId = directAssignee.Id,
                AssignedByUserId = null, // sistem ataması
                AssignedAtUtc = now,
                Note = "Kişiye özel mail — otomatik atandı"
            });
        }

        foreach (var w in result.Warnings)
        {
            _db.TicketParseWarnings.Add(new TicketParseWarning
            {
                TicketId = ticket.Id,
                GmailMessageId = mail.GmailMessageId,
                Code = w.Code,
                Severity = w.Severity,
                Message = w.Message,
                FieldName = w.FieldName,
                SubjectValue = Truncate(w.SubjectValue, 500),
                BodyValue = Truncate(w.BodyValue, 500),
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(ct);

        return new IngestOutcome(IngestOutcomeKind.Created, ticket.ExternalTicketNumber, null, result.Warnings.Count);
    }

    /// <summary>
    /// Ticket maili <b>tek</b> bir kişiye gönderilmişse ve o adres tanınan aktif bir kullanıcıya
    /// aitse, sorumlu zaten bellidir. Birden fazla alıcı varsa mail bir gruba gitmiştir ve
    /// atamayı müdür yapar — bu ayrım bilinçlidir, tahmin yürütülmez.
    /// </summary>
    private async Task<User?> ResolveDirectAssigneeAsync(ParsedTicket parsed, CancellationToken ct)
    {
        if (!_options.AutoAssignDirectTickets) return null;
        if (parsed.OriginalRecipients.Count != 1) return null;

        var address = parsed.OriginalRecipients[0].Trim();
        if (string.IsNullOrWhiteSpace(address)) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == address && u.IsActive, ct);

        if (user is null)
        {
            _logger.LogInformation(
                "Kişiye özel ticket ({Number}) ancak alıcı sistemde tanımlı değil: {Address}. Atanmamış bırakıldı.",
                parsed.ExternalTicketNumber, address);
        }

        return user;
    }

    private async Task<Ticket?> FindExistingTicketAsync(ParsedTicket parsed, CancellationToken ct)
    {
        // #2 — ExternalTicketNumber
        var byNumber = await _db.Tickets
            .FirstOrDefaultAsync(t => t.ExternalTicketNumber == parsed.ExternalTicketNumber, ct);
        if (byNumber is not null) return byNumber;

        // #3 — SourceRequestId
        if (!string.IsNullOrWhiteSpace(parsed.SourceRequestId))
        {
            var byRequestId = await _db.Tickets
                .FirstOrDefaultAsync(t => t.SourceRequestId == parsed.SourceRequestId, ct);
            if (byRequestId is not null) return byRequestId;
        }

        // #4 — Subject + OriginalSentAt (dakika hassasiyeti)
        var minute = new DateTime(
            parsed.OriginalSentAtUtc.Year, parsed.OriginalSentAtUtc.Month, parsed.OriginalSentAtUtc.Day,
            parsed.OriginalSentAtUtc.Hour, parsed.OriginalSentAtUtc.Minute, 0, DateTimeKind.Utc);
        var nextMinute = minute.AddMinutes(1);

        var bySubject = await _db.TicketMailSources
            .Where(s => s.Subject == parsed.OriginalSubject
                        && s.OriginalSentAtUtc >= minute
                        && s.OriginalSentAtUtc < nextMinute)
            .Select(s => s.Ticket)
            .FirstOrDefaultAsync(ct);

        return bySubject;
    }

    private void AddMailSource(Guid ticketId, RawTicketMail mail, ParsedTicket parsed, string sourceMailbox)
    {
        _db.TicketMailSources.Add(new TicketMailSource
        {
            TicketId = ticketId,
            SourceMailbox = sourceMailbox,
            GmailMessageId = mail.GmailMessageId,
            GmailThreadId = mail.GmailThreadId,
            Subject = Truncate(parsed.OriginalSubject, 500)!,
            OriginalSender = parsed.OriginalSender,
            OriginalRecipients = JsonSerializer.Serialize(parsed.OriginalRecipients),
            ForwardedBy = parsed.ForwardedBy,
            IsForwarded = parsed.IsForwarded,
            OriginalSentAtUtc = parsed.OriginalSentAtUtc,
            ReceivedAtUtc = mail.ReceivedAtUtc,
            IngestedAtUtc = _clock.UtcNow
        });
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
