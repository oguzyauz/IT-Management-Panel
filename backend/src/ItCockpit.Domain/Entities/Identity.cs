namespace ItCockpit.Domain.Entities;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public Guid? ManagerUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User? ManagerUser { get; set; }
    public ICollection<User> Members { get; set; } = new List<User>();
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Title { get; set; }
    public Guid? TeamId { get; set; }

    /// <summary>
    /// Kullanıcı devre dışı bırakma mekanizması. Kullanıcılar <b>silinmez</b> —
    /// atama, durum geçmişi ve denetim kayıtları tarihsel olarak korunmak zorunda olduğu için
    /// User üzerinde soft-delete global query filter'ı bilinçli olarak kullanılmaz.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// PBKDF2 türetilmiş parola özeti. <c>null</c> ise kullanıcı henüz parola belirlememiştir
    /// ve giriş yapamaz — yöneticinin bir başlangıç parolası vermesi gerekir.
    /// Parolanın kendisi hiçbir yerde saklanmaz.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>Yönetici başlangıç parolası verdiğinde <c>true</c>; ilk girişte değiştirtilir.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Ard arda başarısız giriş sayısı. Başarılı girişte sıfırlanır.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Çok sayıda hatalı denemeden sonra girişin yeniden açılacağı an.</summary>
    public DateTime? LockedUntilUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Team? Team { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}

/// <summary>
/// Sunucu tarafında tutulan oturum. Token istemcide saklanır ama doğrulama her istekte
/// buradan yapılır; böylece yönetici bir kullanıcıyı pasifleştirdiğinde oturumu da düşer.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Token'ın kendisi değil, SHA-256 özeti saklanır.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public User User { get; set; } = null!;
}

public class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
