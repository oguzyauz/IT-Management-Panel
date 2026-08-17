using FluentAssertions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// Parola ile giriş, oturum yönetimi ve yönetici tarafından kullanıcı yönetimi.
/// Uygulama ekibe ağdan açıldığı için bu kurallar tek koruma katmanıdır.
/// </summary>
public sealed class AccountTests : IDisposable
{
    private const string ManagerEmail = "ayilmaz@menarini.com.tr";
    private const string EmployeeEmail = "doz@menarini.com.tr";
    private const string GoodPassword = "Menarini2026!";

    private readonly ServiceTestHarness _h;

    public AccountTests() => _h = new ServiceTestHarness();

    public void Dispose() => _h.Dispose();

    private Task<UserDto> SetUpAdminAsync(string password = GoodPassword) =>
        _h.Accounts.CompleteInitialSetupAsync(new InitialSetupRequest(ManagerEmail, password));

    // --- Parola özetleme ------------------------------------------------------------------------

    [Fact]
    public void Password_hash_is_salted_so_the_same_password_never_produces_the_same_hash()
    {
        var first = PasswordHasher.Hash("aynilparola123");
        var second = PasswordHasher.Hash("aynilparola123");

        first.Should().NotBe(second);
        PasswordHasher.Verify("aynilparola123", first).Should().BeTrue();
        PasswordHasher.Verify("aynilparola123", second).Should().BeTrue();
    }

    [Fact]
    public void Password_hash_never_contains_the_password_itself()
    {
        var hash = PasswordHasher.Hash("cokGizliParola");

        hash.Should().NotContain("cokGizliParola");
        hash.Should().StartWith("pbkdf2-sha256$");
    }

    [Theory]
    [InlineData("")]
    [InlineData("bozuk")]
    [InlineData("pbkdf2-sha256$abc$def$ghi")]
    [InlineData("pbkdf2-sha256$1000$!!!gecersiz-base64!!!$xyz")]
    public void Corrupt_hash_rejects_login_instead_of_crashing(string stored)
    {
        // Elle düzenlenmiş bir veritabanı satırı girişi kilitlemeli, sunucuyu değil.
        PasswordHasher.Verify("herhangi", stored).Should().BeFalse();
    }

    // --- İlk kurulum ----------------------------------------------------------------------------

    [Fact]
    public async Task Fresh_database_needs_initial_setup()
    {
        (await _h.Accounts.NeedsInitialSetupAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Initial_setup_sets_the_password_and_grants_admin()
    {
        var user = await SetUpAdminAsync();

        user.Roles.Should().Contain(RoleCodes.Admin);
        (await _h.Accounts.NeedsInitialSetupAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Initial_setup_cannot_be_run_twice()
    {
        await SetUpAdminAsync();

        var act = () => _h.Accounts.CompleteInitialSetupAsync(
            new InitialSetupRequest(EmployeeEmail, GoodPassword));

        // Aksi halde ikinci kullanıcı yöneticiliği devralabilirdi.
        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("ALREADY_SET_UP");
    }

    [Fact]
    public async Task Initial_setup_rejects_a_short_password()
    {
        var act = () => SetUpAdminAsync("kisa");

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("WEAK_PASSWORD");
    }

    // --- Giriş ----------------------------------------------------------------------------------

    [Fact]
    public async Task Correct_password_logs_in_and_returns_a_usable_session()
    {
        await SetUpAdminAsync();

        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        login.Token.Should().NotBeNullOrWhiteSpace();
        login.User.Email.Should().Be(ManagerEmail);
        login.MustChangePassword.Should().BeFalse();

        var resolved = await _h.Accounts.ResolveSessionAsync(login.Token);
        resolved!.Email.Should().Be(ManagerEmail);
    }

    [Fact]
    public async Task Email_comparison_is_case_insensitive_without_turkish_i_problems()
    {
        await SetUpAdminAsync();

        // "AYILMAZ".ToLower() Türkçe kültürde "ayılmaz" verir; adres tutmazdı.
        var login = await _h.Accounts.LoginAsync(new LoginRequest("AYILMAZ@MENARINI.COM.TR", GoodPassword));

        login.User.Email.Should().Be(ManagerEmail);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await SetUpAdminAsync();

        var act = () => _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "yanlisparola"));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Unknown_email_gives_the_same_error_as_a_wrong_password()
    {
        await SetUpAdminAsync();

        var unknown = await Record.ExceptionAsync(() =>
            _h.Accounts.LoginAsync(new LoginRequest("yok@menarini.com.tr", GoodPassword)));

        var wrong = await Record.ExceptionAsync(() =>
            _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "yanlis")));

        // Hangi adreslerin kayıtlı olduğu sızdırılmamalı.
        unknown!.Message.Should().Be(wrong!.Message);
    }

    [Fact]
    public async Task User_without_a_password_cannot_log_in()
    {
        await SetUpAdminAsync();

        var act = () => _h.Accounts.LoginAsync(new LoginRequest(EmployeeEmail, GoodPassword));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Account_locks_after_repeated_wrong_passwords()
    {
        await SetUpAdminAsync();

        for (var i = 0; i < AccountService.MaxFailedAttempts; i++)
        {
            await Record.ExceptionAsync(() =>
                _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "yanlis")));
        }

        // Doğru parola bile kilit süresince kabul edilmez.
        var act = () => _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Lock_expires_and_the_correct_password_works_again()
    {
        await SetUpAdminAsync();

        for (var i = 0; i < AccountService.MaxFailedAttempts; i++)
        {
            await Record.ExceptionAsync(() =>
                _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "yanlis")));
        }

        _h.Clock.Advance(AccountService.LockoutDuration + TimeSpan.FromMinutes(1));

        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));
        login.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Successful_login_clears_earlier_failed_attempts()
    {
        await SetUpAdminAsync();

        await Record.ExceptionAsync(() => _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "yanlis")));
        await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        var user = await _h.Db.Users.SingleAsync(u => u.Email == ManagerEmail);
        user.FailedLoginCount.Should().Be(0);
    }

    // --- Oturum ---------------------------------------------------------------------------------

    [Fact]
    public async Task Session_token_is_not_stored_in_plain_text()
    {
        await SetUpAdminAsync();
        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        var stored = await _h.Db.UserSessions.Select(s => s.TokenHash).ToListAsync();

        stored.Should().NotContain(login.Token);
    }

    [Fact]
    public async Task Expired_session_is_rejected()
    {
        await SetUpAdminAsync();
        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        _h.Clock.Advance(AccountService.SessionLifetime + TimeSpan.FromMinutes(1));

        (await _h.Accounts.ResolveSessionAsync(login.Token)).Should().BeNull();
    }

    [Fact]
    public async Task Logout_invalidates_the_token()
    {
        await SetUpAdminAsync();
        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        await _h.Accounts.LogoutAsync(login.Token);

        (await _h.Accounts.ResolveSessionAsync(login.Token)).Should().BeNull();
    }

    [Fact]
    public async Task Deactivating_a_user_drops_their_open_session()
    {
        await SetUpAdminAsync();

        var created = await _h.Accounts.CreateUserAsync(
            new CreateUserRequest("yeni@menarini.com.tr", "Yeni Kullanici", null, RoleCodes.Employee, GoodPassword));

        var login = await _h.Accounts.LoginAsync(new LoginRequest("yeni@menarini.com.tr", GoodPassword));
        (await _h.Accounts.ResolveSessionAsync(login.Token)).Should().NotBeNull();

        await _h.Accounts.SetActiveAsync(created.Id, isActive: false);

        (await _h.Accounts.ResolveSessionAsync(login.Token)).Should().BeNull();
    }

    // --- Parola değiştirme ----------------------------------------------------------------------

    [Fact]
    public async Task Changing_the_password_keeps_the_current_session_and_drops_the_others()
    {
        await SetUpAdminAsync();

        var user = await _h.Db.Users.SingleAsync(u => u.Email == ManagerEmail);

        var stayingSession = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));
        var otherSession = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));

        await _h.Accounts.ChangePasswordAsync(
            user.Id,
            new ChangePasswordRequest(GoodPassword, "YeniParola2026!"),
            currentToken: stayingSession.Token);

        (await _h.Accounts.ResolveSessionAsync(stayingSession.Token)).Should().NotBeNull();
        (await _h.Accounts.ResolveSessionAsync(otherSession.Token)).Should().BeNull();
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        await SetUpAdminAsync();
        var user = await _h.Db.Users.SingleAsync(u => u.Email == ManagerEmail);

        var act = () => _h.Accounts.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest("bilmiyorum", "YeniParola2026!"), null);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task New_password_cannot_be_the_same_as_the_old_one()
    {
        await SetUpAdminAsync();
        var user = await _h.Db.Users.SingleAsync(u => u.Email == ManagerEmail);

        var act = () => _h.Accounts.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest(GoodPassword, GoodPassword), null);

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("SAME_PASSWORD");
    }

    [Fact]
    public async Task New_password_takes_effect_on_the_next_login()
    {
        await SetUpAdminAsync();
        var user = await _h.Db.Users.SingleAsync(u => u.Email == ManagerEmail);

        await _h.Accounts.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest(GoodPassword, "YeniParola2026!"), null);

        var act = () => _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, GoodPassword));
        await act.Should().ThrowAsync<DomainRuleException>();

        var login = await _h.Accounts.LoginAsync(new LoginRequest(ManagerEmail, "YeniParola2026!"));
        login.Token.Should().NotBeNullOrWhiteSpace();
    }

    // --- Yönetici işlemleri ---------------------------------------------------------------------

    [Fact]
    public async Task Created_user_must_change_the_initial_password_on_first_login()
    {
        await SetUpAdminAsync();

        await _h.Accounts.CreateUserAsync(
            new CreateUserRequest("yeni@menarini.com.tr", "Yeni Kullanici", "Uzman", RoleCodes.Employee, GoodPassword));

        var login = await _h.Accounts.LoginAsync(new LoginRequest("yeni@menarini.com.tr", GoodPassword));

        // Yönetici başlangıç parolasını bildiği için kullanıcı değiştirmek zorunda.
        login.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        await SetUpAdminAsync();

        var act = () => _h.Accounts.CreateUserAsync(
            new CreateUserRequest(EmployeeEmail, "Kopya", null, RoleCodes.Employee, GoodPassword));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("DUPLICATE_EMAIL");
    }

    [Fact]
    public async Task Unknown_role_is_rejected()
    {
        await SetUpAdminAsync();

        var act = () => _h.Accounts.CreateUserAsync(
            new CreateUserRequest("x@menarini.com.tr", "X", null, "PATRON", GoodPassword));

        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("INVALID_ROLE");
    }

    [Fact]
    public async Task Admin_reset_forces_a_password_change_and_drops_sessions()
    {
        await SetUpAdminAsync();

        var created = await _h.Accounts.CreateUserAsync(
            new CreateUserRequest("yeni@menarini.com.tr", "Yeni", null, RoleCodes.Employee, GoodPassword));

        var login = await _h.Accounts.LoginAsync(new LoginRequest("yeni@menarini.com.tr", GoodPassword));

        await _h.Accounts.ResetPasswordAsync(created.Id, "SifirlananParola1");

        (await _h.Accounts.ResolveSessionAsync(login.Token)).Should().BeNull();

        var again = await _h.Accounts.LoginAsync(new LoginRequest("yeni@menarini.com.tr", "SifirlananParola1"));
        again.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task The_last_admin_cannot_be_deactivated()
    {
        var admin = await SetUpAdminAsync();

        var act = () => _h.Accounts.SetActiveAsync(admin.Id, isActive: false);

        // Aksi halde uygulama yöneticisiz kalır ve kimse kullanıcı ekleyemez.
        (await act.Should().ThrowAsync<DomainRuleException>()).Which.Code.Should().Be("LAST_ADMIN");
    }

    [Fact]
    public async Task An_admin_can_be_deactivated_once_another_admin_exists()
    {
        var admin = await SetUpAdminAsync();

        await _h.Accounts.CreateUserAsync(
            new CreateUserRequest("admin2@menarini.com.tr", "Ikinci Yonetici", null, RoleCodes.Admin, GoodPassword));

        await _h.Accounts.SetActiveAsync(admin.Id, isActive: false);

        var stored = await _h.Db.Users.SingleAsync(u => u.Id == admin.Id);
        stored.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Managed_user_list_never_exposes_password_hashes()
    {
        await SetUpAdminAsync();

        var users = await _h.Accounts.ListUsersAsync();

        var manager = users.Single(u => u.Email == ManagerEmail);
        manager.HasPassword.Should().BeTrue();

        // ManagedUserDto'da parola alanı bulunmamalı — sözleşme kaza eseri genişlemesin.
        typeof(ManagedUserDto).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Password", StringComparison.OrdinalIgnoreCase)
                                      && n != nameof(ManagedUserDto.HasPassword)
                                      && n != nameof(ManagedUserDto.MustChangePassword));
    }
}
