namespace ItCockpit.Application.Contracts;

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Başarılı giriş. <paramref name="MustChangePassword"/> doğruysa istemci kullanıcıyı
/// parola değiştirme ekranına götürür; token geçerlidir ama arayüz başka yere gitmez.
/// </summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, UserDto User, bool MustChangePassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Oturum açmış kullanıcının kendisi hakkındaki bilgisi. <see cref="UserDto"/>'dan ayrı tutulur:
/// <c>MustChangePassword</c> hesap durumudur ve atama listelerinde herkese görünmemelidir.
/// </summary>
public sealed record CurrentUserDto(
    Guid Id, string Email, string DisplayName, string? Title,
    Guid? TeamId, string? TeamName, IReadOnlyList<string> Roles,
    bool MustChangePassword);

/// <summary>İlk açılışta yöneticinin parolasını belirlemesi. Yalnızca hiç parola yokken çalışır.</summary>
public sealed record InitialSetupRequest(string Email, string Password);

/// <summary>
/// Giriş ekranının ne göstereceğini belirler. Kimlik doğrulaması gerektirmez ama
/// hassas bilgi de dönmez — yalnızca uygulamanın hangi aşamada olduğu.
/// </summary>
public sealed record SetupStatusDto(
    bool NeedsInitialSetup,
    string? AdminEmail,
    string AuthProvider);

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string? Title,
    string Role,
    string InitialPassword);

public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>Yönetim ekranı için kullanıcı satırı. Parola özeti <b>hiçbir zaman</b> dönmez.</summary>
public sealed record ManagedUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? Title,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool HasPassword,
    bool MustChangePassword,
    bool IsLockedOut,
    DateTime? LastLoginAtUtc);
