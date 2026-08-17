using System.Security.Claims;
using System.Text.Encodings.Web;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItCockpit.Api.Auth;

/// <summary>
/// Geliştirme kimlik sağlayıcısı. <c>X-Mock-User-Id</c> başlığındaki (veya
/// <c>Authorization: Bearer mock:&lt;guid&gt;</c> içindeki) kullanıcı kimliğini doğrudan kabul eder.
/// <b>Yalnızca geliştirme içindir</b> — üretimde <c>Auth:Provider = Google</c> kullanılır.
/// </summary>
public sealed class MockAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string HeaderName = "X-Mock-User-Id";
    private const string BearerPrefix = "mock:";

    private readonly AppDbContext _db;

    public MockAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw))
        {
            var authorization = Request.Headers.Authorization.FirstOrDefault();
            if (authorization?.StartsWith("Bearer " + BearerPrefix, StringComparison.OrdinalIgnoreCase) == true)
                raw = authorization["Bearer ".Length..][BearerPrefix.Length..];
        }

        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        if (!Guid.TryParse(raw.Trim(), out var userId))
            return AuthenticateResult.Fail("Geçersiz mock kullanıcı kimliği.");

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user is null)
            return AuthenticateResult.Fail("Mock kullanıcı bulunamadı veya aktif değil.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName)
        };

        claims.AddRange(user.UserRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Role.Code)));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Mock);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthSchemes.Mock));
    }
}
