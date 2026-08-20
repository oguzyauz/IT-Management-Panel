using System.Security.Cryptography;
using System.Text;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItCockpit.Application.Services;

/// <summary>
/// Parola ile giriş, oturum yönetimi ve yönetici tarafından kullanıcı yönetimi.
///
/// Uygulama şirket ağında tek bir makinede çalışıp ekibe tarayıcıdan açıldığı için
/// kimlik doğrulaması <b>yerel</b>dir: hesaplar bu veritabanında durur, dış bir kimlik
/// sağlayıcısına bağlanılmaz.
/// </summary>
public sealed class AccountService
{
    /// <summary>Kaç hatalı denemeden sonra hesap geçici olarak kilitlenir.</summary>
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);

    public const int MinPasswordLength = 8;

    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public AccountService(IAppDbContext db, IClock clock, IAuditLogger audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    // --- Kurulum durumu -------------------------------------------------------------------------

    /// <summary>
    /// Uygulama hiç kurulmamışsa (hiçbir kullanıcının parolası yoksa) giriş ekranı yerine
    /// ilk kurulum ekranı gösterilir.
    /// </summary>
    public async Task<bool> NeedsInitialSetupAsync(CancellationToken ct = default) =>
        !await _db.Users.AnyAsync(u => u.PasswordHash != null, ct);

    /// <summary>
    /// İlk yönetici parolasını belirler ve hesabı ADMIN rolüne yükseltir.
    /// Yalnızca sistemde hiç parola yokken çalışır — sonrasında bu uç kapanır,
    /// aksi halde herkes yöneticiliği devralabilirdi.
    /// </summary>
    public async Task<UserDto> CompleteInitialSetupAsync(InitialSetupRequest request, CancellationToken ct = default)
    {
        if (!await NeedsInitialSetupAsync(ct))
            throw new DomainRuleException("ALREADY_SET_UP", "Kurulum zaten tamamlanmış.");

        ValidatePassword(request.Password);

        var email = NormalizeEmail(request.Email);

        var user = await _db.Users
                       .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                       .FirstOrDefaultAsync(u => u.Email == email, ct)
                   ?? throw new DomainRuleException(
                       "USER_NOT_FOUND",
                       $"{request.Email} adresiyle tanımlı bir kullanıcı yok.");

        var now = _clock.UtcNow;

        user.PasswordHash = PasswordHasher.Hash(request.Password);
        user.MustChangePassword = false;
        user.PasswordChangedAtUtc = now;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.IsActive = true;
        user.UpdatedAtUtc = now;

        await EnsureRoleAsync(user, RoleCodes.Admin, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("INITIAL_SETUP", "User", user.Id.ToString(), new { user.Email }, ct);

        return await LoadUserDtoAsync(user.Id, ct);
    }

    // --- Giriş ----------------------------------------------------------------------------------

    /// <summary>
    /// E-posta ve parolayla giriş. Hatalı parola, olmayan kullanıcı ve pasif kullanıcı
    /// <b>aynı</b> mesajı döndürür — hangi adreslerin kayıtlı olduğu sızdırılmaz.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var now = _clock.UtcNow;

        var user = await _db.Users
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || user.PasswordHash is null)
            throw new DomainRuleException("INVALID_CREDENTIALS", "E-posta veya parola hatalı.");

        if (user.LockedUntilUtc is { } until && until > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            throw new DomainRuleException(
                "ACCOUNT_LOCKED",
                $"Çok fazla hatalı deneme yapıldı. {minutes} dakika sonra tekrar deneyin.");
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntilUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            user.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(ct);

            throw new DomainRuleException("INVALID_CREDENTIALS", "E-posta veya parola hatalı.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        var (token, session) = CreateSession(user.Id, now);
        _db.UserSessions.Add(session);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LOGIN", "User", user.Id.ToString(), null, ct);

        return new LoginResponse(
            token,
            session.ExpiresAtUtc,
            ToDto(user),
            user.MustChangePassword);
    }

    /// <summary>
    /// LDAP/AD ile doğrulama. E-postaya karşılık gelen kullanıcı DB'de aktif ve
    /// rolü tanımlı olmalıdır; yoksa giriş reddedilir. Parola doğrulama işlemi
    /// ILdapAuthenticator soyutlaması üzerinden yapılır.
    /// </summary>
    public async Task<LoginResponse> LoginWithLdapAsync(
        LoginRequest request, ILdapAuthenticator ldap, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var now = _clock.UtcNow;

        var user = await _db.Users
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !user.UserRoles.Any())
            throw new DomainRuleException("INVALID_CREDENTIALS",
                "E-posta veya parola hatalı.");

        // Hesap kilidi kontrolü (brute force koruması)
        if (user.LockedUntilUtc is { } until && until > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
            throw new DomainRuleException(
                "ACCOUNT_LOCKED",
                $"Çok fazla hatalı deneme yapıldı. {minutes} dakika sonra tekrar deneyin.");
        }

        // LDAP Bind ile parola doğrulama
        var ldapResult = await ldap.AuthenticateAsync(email, request.Password, ct);
        if (!ldapResult.Success)
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntilUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }

            user.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(ct);

            throw new DomainRuleException("INVALID_CREDENTIALS",
                "E-posta veya parola hatalı.");
        }

        // AD'den gelen DisplayName ile DB senkronizasyonu
        if (ldapResult.DisplayName is not null && user.DisplayName != ldapResult.DisplayName)
            user.DisplayName = ldapResult.DisplayName;

        // Başarılı giriş — session oluştur
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        var (token, session) = CreateSession(user.Id, now);
        _db.UserSessions.Add(session);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LOGIN_LDAP", "User", user.Id.ToString(), null, ct);

        return new LoginResponse(
            token,
            session.ExpiresAtUtc,
            ToDto(user),
            false);
    }

    /// <summary>
    /// Token'ı doğrular ve oturumun sahibini döner. Süresi geçmiş, iptal edilmiş veya
    /// sahibi pasifleştirilmiş oturumlar kabul edilmez.
    /// </summary>
    public async Task<User?> ResolveSessionAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = HashToken(token);
        var now = _clock.UtcNow;

        var session = await _db.UserSessions
            .Include(s => s.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now)
            return null;

        if (!session.User.IsActive) return null;

        // Son görülme her istekte yazılmaz; sadece belirgin şekilde eskidiyse güncellenir.
        // Aksi halde her sayfa yenilemesi bir UPDATE üretirdi.
        if (now - session.LastSeenAtUtc > TimeSpan.FromMinutes(5))
        {
            session.LastSeenAtUtc = now;
            await _db.SaveChangesAsync(ct);
        }

        return session.User;
    }

    public async Task LogoutAsync(string token, CancellationToken ct = default)
    {
        var hash = HashToken(token);

        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session is null || session.RevokedAtUtc is not null) return;

        session.RevokedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // --- Parola ---------------------------------------------------------------------------------

    /// <summary>
    /// Kullanıcının kendi parolasını değiştirmesi. Mevcut parola doğrulanır ve
    /// <b>diğer tüm oturumları</b> kapatılır — parola değiştirmenin amacı budur.
    /// </summary>
    public async Task ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, string? currentToken, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new DomainRuleException("INVALID_CREDENTIALS", "Mevcut parola hatalı.");

        ValidatePassword(request.NewPassword);

        if (PasswordHasher.Verify(request.NewPassword, user.PasswordHash))
            throw new DomainRuleException("SAME_PASSWORD", "Yeni parola eskisiyle aynı olamaz.");

        var now = _clock.UtcNow;

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAtUtc = now;
        user.UpdatedAtUtc = now;

        await RevokeOtherSessionsAsync(userId, currentToken, now, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PASSWORD_CHANGED", "User", userId.ToString(), null, ct);
    }

    // --- Yönetici işlemleri ---------------------------------------------------------------------

    public async Task<IReadOnlyList<ManagedUserDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(ct);

        return users.Select(u => new ManagedUserDto(
            u.Id, u.Email, u.DisplayName, u.Title,
            u.UserRoles.Select(ur => ur.Role.Code).ToList(),
            u.IsActive,
            u.PasswordHash is not null,
            u.MustChangePassword,
            u.LockedUntilUtc is { } until && until > now,
            u.LastLoginAtUtc)).ToList();
    }

    /// <summary>
    /// Yönetici yeni kullanıcı açar ve ona bir başlangıç parolası verir. Kullanıcı ilk
    /// girişinde parolayı değiştirmek zorundadır, böylece yönetici parolayı bilmeye devam etmez.
    /// </summary>
    public async Task<ManagedUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);

        if (!email.Contains('@'))
            throw new DomainRuleException("INVALID_EMAIL", "Geçerli bir e-posta adresi girin.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new DomainRuleException("NAME_REQUIRED", "Ad soyad zorunlu.");

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new DomainRuleException("DUPLICATE_EMAIL", "Bu e-posta adresi zaten kayıtlı.");

        var role = NormalizeRole(request.Role);
        if (request.InitialPassword is not null)
            ValidatePassword(request.InitialPassword);

        var now = _clock.UtcNow;

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            IsActive = true,
            PasswordHash = request.InitialPassword is not null
                ? PasswordHasher.Hash(request.InitialPassword)
                : null,
            MustChangePassword = request.InitialPassword is not null,
            PasswordChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Users.Add(user);
        await EnsureRoleAsync(user, role, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("USER_CREATED", "User", user.Id.ToString(), new { user.Email, Role = role }, ct);

        return (await ListUsersAsync(ct)).First(u => u.Id == user.Id);
    }

    /// <summary>Yönetici parolayı sıfırlar. Kullanıcının açık oturumları kapatılır.</summary>
    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        var now = _clock.UtcNow;

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = true;
        user.PasswordChangedAtUtc = now;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedAtUtc = now;

        await RevokeOtherSessionsAsync(userId, currentToken: null, now, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PASSWORD_RESET", "User", userId.ToString(), null, ct);
    }

    /// <summary>
    /// Kullanıcıyı aktif/pasif yapar. Kayıtlar silinmez; pasif kullanıcı giriş yapamaz,
    /// açık oturumları düşer ve yeni ticket ataması alamaz ama geçmişte görünmeye devam eder.
    /// </summary>
    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken ct = default)
    {
        var user = await _db.Users
                       .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                       .FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        if (!isActive && user.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin))
        {
            var otherAdmins = await _db.Users
                .Where(u => u.Id != userId && u.IsActive)
                .Where(u => u.UserRoles.Any(ur => ur.Role.Code == RoleCodes.Admin))
                .CountAsync(ct);

            if (otherAdmins == 0)
                throw new DomainRuleException(
                    "LAST_ADMIN",
                    "Tek yöneticiyi pasifleştiremezsiniz — önce başka bir yönetici tanımlayın.");
        }

        var now = _clock.UtcNow;

        user.IsActive = isActive;
        user.UpdatedAtUtc = now;

        if (!isActive)
            await RevokeOtherSessionsAsync(userId, currentToken: null, now, ct);

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(isActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", "User", userId.ToString(), null, ct);
    }

    // --- Yardımcılar ----------------------------------------------------------------------------

    public static void ValidatePassword(string? password)
    {
        var (isValid, errorMessage) = UserRegistrationValidator.ValidatePassword(password ?? string.Empty);
        if (!isValid)
            throw new DomainRuleException("WEAK_PASSWORD", errorMessage);
    }

    /// <summary>
    /// Türkçe kültürde <c>"I".ToLower()</c> "ı" verir; e-posta karşılaştırması bozulur.
    /// Bu yüzden her yerde kültürden bağımsız küçültme kullanılır.
    /// </summary>
    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeRole(string? role)
    {
        var value = (role ?? string.Empty).Trim().ToUpperInvariant();

        return value switch
        {
            RoleCodes.Admin or RoleCodes.Manager or RoleCodes.Employee => value,
            _ => throw new DomainRuleException("INVALID_ROLE", "Rol ADMIN, MANAGER veya EMPLOYEE olmalı.")
        };
    }

    private async Task EnsureRoleAsync(User user, string roleCode, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, ct)
                   ?? throw new DomainRuleException("ROLE_NOT_FOUND", $"Rol bulunamadı: {roleCode}");

        var already = user.UserRoles.Any(ur => ur.RoleId == role.Id)
                      || await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);

        if (!already)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
    }

    private async Task RevokeOtherSessionsAsync(Guid userId, string? currentToken, DateTime now, CancellationToken ct)
    {
        var keepHash = currentToken is null ? null : HashToken(currentToken);

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            if (keepHash is not null && session.TokenHash == keepHash) continue;
            session.RevokedAtUtc = now;
        }
    }

    private (string Token, UserSession Session) CreateSession(Guid userId, DateTime now)
    {
        // 32 bayt kriptografik rastgelelik — tahmin edilemez.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return (token, new UserSession
        {
            UserId = userId,
            TokenHash = HashToken(token),
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = now.Add(SessionLifetime)
        });
    }

    /// <summary>
    /// Token veritabanında açık saklanmaz: veritabanı sızarsa oturumlar devralınabilirdi.
    /// Token yüksek entropili rastgele bir değer olduğu için tuz gerekmez.
    /// </summary>
    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<UserDto> LoadUserDtoAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Team)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ct);

        return ToDto(user);
    }

    private static UserDto ToDto(User user) => new(
        user.Id, user.Email, user.DisplayName, user.Title,
        user.TeamId, user.Team?.Name,
        user.UserRoles.Select(ur => ur.Role.Code).ToList());
}
