using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using ItCockpit.Infrastructure.Gmail;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/ingestion")]
[Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
public sealed class IngestionController : ControllerBase
{
    private readonly TicketIngestionService _ingestion;
    private readonly AppDbContext _db;
    private readonly IGmailTicketSource _source;
    private readonly GmailCredentialsInspector _inspector;
    private readonly MailboxRegistry _mailboxes;

    public IngestionController(
        TicketIngestionService ingestion, AppDbContext db,
        IGmailTicketSource source, GmailCredentialsInspector inspector,
        MailboxRegistry mailboxes)
    {
        _ingestion = ingestion;
        _db = db;
        _source = source;
        _inspector = inspector;
        _mailboxes = mailboxes;
    }

    /// <summary>
    /// Gmail kurulumunun hangi aşamada olduğunu söyler: credentials.json yerinde mi, doğru tipte mi,
    /// yetkilendirme yapılmış mı ve sıradaki adım ne. Sır döndürmez.
    /// </summary>
    [HttpGet("gmail-status")]
    public async Task<ActionResult<GmailSetupStatus>> GmailStatus(CancellationToken ct) =>
        Ok(_inspector.Inspect(await _mailboxes.GetAsync(ct)));

    /// <summary>Okunacak posta kutuları. Yönetim ekranından düzenlenir.</summary>
    [HttpGet("mailboxes")]
    public async Task<ActionResult<IReadOnlyList<string>>> Mailboxes(CancellationToken ct) =>
        Ok(await _mailboxes.GetAsync(ct));

    [HttpPost("mailboxes")]
    public async Task<ActionResult<IReadOnlyList<string>>> AddMailbox(
        [FromBody] MailboxRequest request, CancellationToken ct) =>
        Ok(await _mailboxes.AddAsync(request.Mailbox, ct));

    [HttpDelete("mailboxes")]
    public async Task<ActionResult<IReadOnlyList<string>>> RemoveMailbox(
        [FromQuery] string mailbox, CancellationToken ct) =>
        Ok(await _mailboxes.RemoveAsync(mailbox, ct));

    /// <summary>
    /// Kutunun okuma penceresini sıfırlar; sonraki okuma baştan tarar.
    /// Kutu bağlı göründüğü hâlde eski mailler gelmiyorsa kullanılır.
    /// </summary>
    [HttpPost("mailboxes/rescan")]
    public async Task<IActionResult> RescanMailbox([FromQuery] string mailbox, CancellationToken ct)
    {
        await _mailboxes.ResetSyncStateAsync(mailbox, ct);
        return NoContent();
    }

    /// <summary>
    /// Kuru çalıştırma: mailleri okur ve ayrıştırır ama <b>kaydetmez</b>.
    /// Gerçek kutuya bağlanırken parser'ın ne gördüğünü teşhis etmek için.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<IngestionPreviewDto>> Preview(
        [FromQuery] int? maxResults, CancellationToken ct) =>
        Ok(await _ingestion.PreviewAsync(maxResults, ct));

    /// <summary>Gmail okuma işini elle tetikler (zamanlanmış job'a ek olarak).</summary>
    [HttpPost("run")]
    public async Task<ActionResult<IngestionRunResultDto>> Run(CancellationToken ct) =>
        Ok(await _ingestion.RunAsync(ct));

    /// <summary>
    /// Gmail OAuth onayını tetikler. İlk çağrıda sunucunun çalıştığı makinede tarayıcı açılır ve
    /// kullanıcı onay verene kadar beklenir. Token diske yazıldıktan sonra tekrar gerekmez.
    /// Mock sağlayıcıda kullanılamaz.
    /// </summary>
    [HttpPost("authorize")]
    public async Task<ActionResult<GmailAuthorizeResult>> Authorize(
        [FromQuery] string? mailbox, CancellationToken ct)
    {
        if (_source is not IGmailAuthorizer authorizer)
        {
            return BadRequest(new
            {
                message = $"Aktif Gmail sağlayıcısı '{_source.ProviderName}' yetkilendirme gerektirmiyor. " +
                          "Gerçek Gmail için Gmail:Provider ayarını 'Google' yapın."
            });
        }

        var configured = await _mailboxes.GetAsync(ct);
        var target = string.IsNullOrWhiteSpace(mailbox) ? configured[0] : mailbox.Trim();

        if (!configured.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = $"'{target}' yapılandırılmış kutular arasında değil. " +
                          $"Tanımlı kutular: {string.Join(", ", configured)}"
            });
        }

        var email = await authorizer.AuthorizeAsync(target, ct);

        // Kullanıcı tarayıcıda başka bir hesapla giriş yapmış olabilir; bu sessizce yanlış
        // kutunun okunmasına yol açar, o yüzden açıkça bildirilir.
        var matches = string.Equals(email, target, StringComparison.OrdinalIgnoreCase);

        return Ok(new GmailAuthorizeResult(email, target, matches, _source.ProviderName));
    }

    /// <summary>Yapılandırılmış her posta kutusunun senkron durumu.</summary>
    [HttpGet("state")]
    public async Task<ActionResult<IReadOnlyList<GmailSyncStateDto>>> State(CancellationToken ct)
    {
        var states = await _db.GmailSyncStates.AsNoTracking().ToListAsync(ct);

        var result = (await _mailboxes.GetAsync(ct)).Select(mailbox =>
        {
            var s = states.FirstOrDefault(x =>
                string.Equals(x.MailboxAddress, mailbox, StringComparison.OrdinalIgnoreCase));

            return s is null
                ? new GmailSyncStateDto(mailbox, null, null, null, null, 0, 0, 0, 0)
                : new GmailSyncStateDto(
                    s.MailboxAddress, s.LastHistoryId, s.LastSyncCompletedAtUtc,
                    s.LastSyncStatus, s.LastError,
                    s.MessagesSeen, s.TicketsCreated, s.DuplicatesSkipped, s.MailsRejected);
        }).ToList();

        return Ok(result);
    }
}

public sealed record MailboxRequest(string Mailbox);

public sealed record GmailAuthorizeResult(
    string AuthorizedEmail,
    string RequestedMailbox,
    bool MatchesRequestedMailbox,
    string Provider);

[ApiController]
[Route("api/settings")]
[Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
public sealed class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAppSettingsProvider _settings;
    private readonly ICurrentUser _currentUser;

    public SettingsController(AppDbContext db, IAppSettingsProvider settings, ICurrentUser currentUser)
    {
        _db = db;
        _settings = settings;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppSettingDto>>> Get(CancellationToken ct) =>
        Ok(await _db.AppSettings.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .Select(s => new AppSettingDto(s.Key, s.Value, s.DataType, s.Category, s.Description))
            .ToListAsync(ct));

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<AppSettingDto>>> Update(
        [FromBody] UpdateAppSettingsRequest request, CancellationToken ct)
    {
        foreach (var (key, value) in request.Values)
            await _settings.SetAsync(key, value, _currentUser.UserId, ct);

        return await Get(ct);
    }
}

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AccountService _accounts;

    public UsersController(AppDbContext db, AccountService accounts)
    {
        _db = db;
        _accounts = accounts;
    }

    /// <summary>Atama listelerini besleyen hafif liste — herkese açıktır.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Get(CancellationToken ct) =>
        Ok(await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDto(
                u.Id, u.Email, u.DisplayName, u.Title, u.TeamId,
                u.Team != null ? u.Team.Name : null,
                u.UserRoles.Select(ur => ur.Role.Code).ToList()))
            .ToListAsync(ct));

    /// <summary>Yönetim ekranı listesi: hesap durumu ve parola bilgisi içerir.</summary>
    [HttpGet("managed")]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<ActionResult<IReadOnlyList<ManagedUserDto>>> GetManaged(CancellationToken ct) =>
        Ok(await _accounts.ListUsersAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<ActionResult<ManagedUserDto>> Create(
        [FromBody] CreateUserRequest request, CancellationToken ct) =>
        Ok(await _accounts.CreateUserAsync(request, ct));

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<IActionResult> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _accounts.ResetPasswordAsync(id, request.NewPassword, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/active")]
    [Authorize(Roles = $"{RoleCodes.Manager},{RoleCodes.Admin}")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        await _accounts.SetActiveAsync(id, value, ct);
        return NoContent();
    }
}
