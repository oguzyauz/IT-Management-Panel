using System.Security.Claims;
using System.Text.Encodings.Web;
using ItCockpit.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ItCockpit.Api.Auth;

/// <summary>
/// Parola ile giriş sonrası verilen oturum token'ını doğrular.
/// Token <c>Authorization: Bearer &lt;token&gt;</c> başlığıyla gelir ve her istekte
/// veritabanından kontrol edilir — böylece pasifleştirilen kullanıcının oturumu anında düşer.
/// </summary>
public sealed class LocalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AccountService _accounts;

    public LocalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AccountService accounts)
        : base(options, logger, encoder)
    {
        _accounts = accounts;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.NoResult();

        var user = await _accounts.ResolveSessionAsync(token, Context.RequestAborted);

        // Süresi dolmuş veya iptal edilmiş token: Fail değil NoResult da olabilirdi ama
        // Fail, istemciye 401 döndürüp oturumu temizletmek için daha doğru sinyal.
        if (user is null)
            return AuthenticateResult.Fail("Oturum geçersiz veya süresi dolmuş.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName)
        };

        claims.AddRange(user.UserRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Role.Code)));

        var identity = new ClaimsIdentity(claims, AuthSchemes.Local);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.Local));
    }
}
