using System.Security.Claims;
using FluentAssertions;
using ItCockpit.Api.Auth;
using ItCockpit.Domain;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ItCockpit.Tests;

/// <summary>
/// <see cref="HttpCurrentUser"/> tüm yetki kontrollerinin dayandığı adaptördür.
/// Buradaki bir hata sunucu tarafındaki kapsam daraltmasını sessizce düşürür,
/// bu yüzden ayrıca sınanır.
/// </summary>
public sealed class HttpCurrentUserTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ClaimsPrincipal Authenticated() => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Email, "ayilmaz@menarini.com.tr"),
            new Claim(ClaimTypes.Name, "Ahmet Yılmaz"),
            new Claim(ClaimTypes.Role, RoleCodes.Manager)
        ],
        authenticationType: "Local"));

    [Fact]
    public void Reads_identity_from_the_current_request()
    {
        var context = new DefaultHttpContext { User = Authenticated() };
        var currentUser = new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });

        currentUser.UserId.Should().Be(UserId);
        currentUser.Email.Should().Be("ayilmaz@menarini.com.tr");
        currentUser.DisplayName.Should().Be("Ahmet Yılmaz");
        currentUser.Roles.Should().Contain(RoleCodes.Manager);
        currentUser.IsInRole(RoleCodes.Manager).Should().BeTrue();
    }

    [Fact]
    public void Sees_the_identity_even_when_it_is_assigned_after_construction()
    {
        // Gerçekte olan bu: parola ile girişte kimlik doğrulama handler'ı
        // AccountService -> IAuditLogger -> ICurrentUser zincirini tetikliyor, yani
        // bu scoped nesne HttpContext.User atanmadan ÖNCE oluşuyor. Principal kurucuda
        // yakalanırsa istek yetkili olduğu hâlde kullanıcı ve roller boş görünür ve
        // her uç "Oturum açmış kullanıcı bulunamadı" döner.
        var context = new DefaultHttpContext();
        var currentUser = new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });

        currentUser.UserId.Should().BeNull();

        context.User = Authenticated();

        currentUser.UserId.Should().Be(UserId);
        currentUser.IsInRole(RoleCodes.Manager).Should().BeTrue();
    }

    [Fact]
    public void Is_anonymous_when_there_is_no_request()
    {
        // Arka plan job'ları HttpContext olmadan çalışır; patlamak yerine anonim görünmeli.
        var currentUser = new HttpCurrentUser(new HttpContextAccessor { HttpContext = null });

        currentUser.UserId.Should().BeNull();
        currentUser.Email.Should().BeNull();
        currentUser.Roles.Should().BeEmpty();
        currentUser.IsInRole(RoleCodes.Manager).Should().BeFalse();
    }
}
