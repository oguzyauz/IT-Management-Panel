using ItCockpit.Api.Auth;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItCockpit.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthOptions _options;
    private readonly AccountService _accounts;

    public AuthController(AppDbContext db, IOptions<AuthOptions> options, AccountService accounts)
    {
        _db = db;
        _options = options.Value;
        _accounts = accounts;
    }

    /// <summary>
    /// Giriş ekranının ne göstereceğini belirler: ilk kurulum mu, parola girişi mi,
    /// yoksa geliştirme modundaki kullanıcı seçimi mi.
    /// </summary>
    [HttpGet("setup-status")]
    [AllowAnonymous]
    public async Task<ActionResult<SetupStatusDto>> SetupStatus(CancellationToken ct)
    {
        var needsSetup = IsLocalProvider() && await _accounts.NeedsInitialSetupAsync(ct);

        // İlk kurulumda hangi hesabın yönetici olacağını göstermek için seed'deki
        // yönetici adresi verilir; parola veya başka bir bilgi dönmez.
        string? adminEmail = null;
        if (needsSetup)
        {
            adminEmail = await _db.Users.AsNoTracking()
                .Where(u => u.UserRoles.Any(ur => ur.Role.Code == Domain.RoleCodes.Manager
                                                  || ur.Role.Code == Domain.RoleCodes.Admin))
                .OrderBy(u => u.CreatedAtUtc)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
        }

        return Ok(new SetupStatusDto(needsSetup, adminEmail, _options.Provider));
    }

    /// <summary>İlk açılışta yönetici parolasının belirlenmesi. Kurulum tamamlanınca kapanır.</summary>
    [HttpPost("initial-setup")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> InitialSetup([FromBody] InitialSetupRequest request, CancellationToken ct)
    {
        if (!IsLocalProvider())
            return NotFound(new { message = "Parola ile giriş etkin değil." });

        return Ok(await _accounts.CompleteInitialSetupAsync(request, ct));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!IsLocalProvider())
            return NotFound(new { message = "Parola ile giriş etkin değil." });

        return Ok(await _accounts.LoginAsync(request, ct));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (BearerToken() is { } token)
            await _accounts.LogoutAsync(token, ct);

        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return Unauthorized();

        await _accounts.ChangePasswordAsync(userId, request, BearerToken(), ct);
        return NoContent();
    }

    /// <summary>İstekteki oturum token'ı — parola değişiminde bu oturum ayakta bırakılır.</summary>
    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.FirstOrDefault();

        return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    private bool IsLocalProvider() =>
        string.Equals(_options.Provider, "Local", StringComparison.OrdinalIgnoreCase);

    /// <summary>Geliştirmede giriş ekranında listelenecek kullanıcılar.</summary>
    [HttpGet("mock-users")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetMockUsers(CancellationToken ct)
    {
        if (!IsMockProvider())
            return NotFound(new { message = "Mock kimlik sağlayıcısı etkin değil." });

        var users = await LoadUsersAsync(ct);
        return Ok(users);
    }

    /// <summary>
    /// Mock giriş. Gerçek bir kimlik doğrulaması yapmaz; verilen kullanıcı kimliğini
    /// sonraki isteklerde kullanılacak token olarak döner.
    /// </summary>
    [HttpPost("mock-login")]
    [AllowAnonymous]
    public async Task<ActionResult<MockLoginResponse>> MockLogin([FromBody] MockLoginRequest request, CancellationToken ct)
    {
        if (!IsMockProvider())
            return NotFound(new { message = "Mock kimlik sağlayıcısı etkin değil." });

        var users = await LoadUsersAsync(ct);

        var user = request.UserId is { } id
            ? users.FirstOrDefault(u => u.Id == id)
            : users.FirstOrDefault(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase));

        if (user is null)
            return Unauthorized(new { message = "Kullanıcı bulunamadı." });

        if (!IsDomainAllowed(user.Email))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bu domain için erişim yok." });

        return Ok(new MockLoginResponse($"mock:{user.Id}", user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(idClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(ct);

        if (user is null) return Unauthorized();

        return Ok(new CurrentUserDto(
            user.Id, user.Email, user.DisplayName, user.Title,
            user.TeamId, user.Team?.Name,
            user.UserRoles.Select(ur => ur.Role.Code).ToList(),
            user.MustChangePassword));
    }

    private bool IsMockProvider() =>
        string.Equals(_options.Provider, "Mock", StringComparison.OrdinalIgnoreCase);

    private bool IsDomainAllowed(string email)
    {
        if (_options.AllowedDomains.Length == 0) return true;

        var at = email.LastIndexOf('@');
        if (at < 0) return false;

        var domain = email[(at + 1)..];
        return _options.AllowedDomains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<UserDto>> LoadUsersAsync(CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDto(
                u.Id, u.Email, u.DisplayName, u.Title, u.TeamId,
                u.Team != null ? u.Team.Name : null,
                u.UserRoles.Select(ur => ur.Role.Code).ToList()))
            .ToListAsync(ct);
}

public sealed record MockLoginRequest(Guid? UserId, string? Email);
public sealed record MockLoginResponse(string Token, UserDto User);
