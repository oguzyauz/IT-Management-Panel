using System.Security.Claims;
using ItCockpit.Application.Abstractions;

namespace ItCockpit.Api.Auth;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <summary>
    /// Principal <b>her erişimde</b> yeniden okunur, kurucuda yakalanmaz.
    ///
    /// Kimlik doğrulama katmanı <c>HttpContext.User</c>'ı istek başladıktan sonra atar.
    /// Parola ile girişte doğrulama handler'ının kendisi <c>AccountService → IAuditLogger →
    /// ICurrentUser</c> zincirini tetikliyor; kurucuda yakalansaydı bu scoped nesne, kimlik
    /// atanmadan önceki anonim principal'ı ömrü boyunca taşır ve istek yetkili olduğu hâlde
    /// kullanıcı/rol boş görünürdü.
    /// </summary>
    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
