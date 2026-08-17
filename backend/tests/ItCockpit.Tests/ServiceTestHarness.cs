using ItCockpit.Application.Services;
using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ItCockpit.Tests;

/// <summary>
/// SQLite bellek içi veritabanı üzerinde servisleri gerçek EF Core ile çalıştırır.
/// InMemory provider yerine SQLite kullanılır; böylece ilişkiler ve kısıtlar gerçekten uygulanır.
/// </summary>
public sealed class ServiceTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }
    public FixedClock Clock { get; }
    public TestCurrentUser CurrentUser { get; }
    public TestAuditLogger Audit { get; }
    public TestAppSettingsProvider Settings { get; }
    public StubGmailTicketSource GmailSource { get; }
    public StubReminderMailSender MailSender { get; }

    public GmailIngestionOptions GmailOptions { get; private set; } = null!;

    public AccountService Accounts { get; }
    public MailboxRegistry Mailboxes { get; }
    public TicketService Tickets { get; }
    public TicketIngestionService Ingestion { get; }
    public WorkScheduleService Schedule { get; }
    public ReminderService Reminders { get; }
    public DashboardService Dashboard { get; }

    public static readonly Guid ManagerId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid EmployeeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static readonly Guid Employee2Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    public ServiceTestHarness(DateTime? utcNow = null, IDictionary<string, string>? settings = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();

        Clock = new FixedClock(utcNow ?? new DateTime(2026, 8, 4, 6, 0, 0));
        CurrentUser = new TestCurrentUser { UserId = ManagerId, Email = "ayilmaz@menarini.com.tr", Roles = [RoleCodes.Manager] };
        Audit = new TestAuditLogger();
        Settings = new TestAppSettingsProvider(settings);
        GmailSource = new StubGmailTicketSource();
        MailSender = new StubReminderMailSender();

        SeedUsers();

        Accounts = new AccountService(Db, Clock, Audit);
        Tickets = new TicketService(Db, Clock, CurrentUser, Settings, Audit);

        // Gerçek kurulumda kutu yapılandırmadan gelir; varsayılanı yoktur (bkz.
        // GmailIngestionOptions.MailboxAddress). Testlerin çoğu okuma yaptığı için burada
        // açıkça bir kutu tanımlanır.
        GmailOptions = new GmailIngestionOptions { MailboxAddress = "ayilmaz@menarini.com.tr" };

        Mailboxes = new MailboxRegistry(Db, Settings, Options.Create(GmailOptions));

        Ingestion = new TicketIngestionService(
            Db, GmailSource, Fixtures.CreateParser(), Clock,
            Options.Create(GmailOptions),
            Options.Create(new ItCockpit.Application.Parsing.TicketMailParserOptions()),
            Mailboxes,
            NullLogger<TicketIngestionService>.Instance);

        Schedule = new WorkScheduleService(Db, Clock, CurrentUser, Settings, Audit);
        Reminders = new ReminderService(
            Db, Clock, CurrentUser, MailSender, Audit, Options.Create(new ReminderOptions()), Tickets);
        Dashboard = new DashboardService(Db, Clock, Tickets, Schedule, Reminders);
    }

    private void SeedUsers()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Db.Roles.AddRange(
            new Role { Id = 1, Code = RoleCodes.Admin, Name = "Sistem Yöneticisi" },
            new Role { Id = 2, Code = RoleCodes.Manager, Name = "Yönetici" },
            new Role { Id = 3, Code = RoleCodes.Employee, Name = "Çalışan" });

        Db.Users.AddRange(
            new User { Id = ManagerId, Email = "ayilmaz@menarini.com.tr", DisplayName = "Ahmet Yılmaz", Title = "IT Müdürü", CreatedAtUtc = now, UpdatedAtUtc = now },
            new User { Id = EmployeeId, Email = "doz@menarini.com.tr", DisplayName = "Dilara Öz", Title = "Destek Uzmanı", CreatedAtUtc = now, UpdatedAtUtc = now },
            new User { Id = Employee2Id, Email = "btufan@menarini.com.tr", DisplayName = "Beril Tufan", Title = "Sistem Uzmanı", CreatedAtUtc = now, UpdatedAtUtc = now });

        Db.UserRoles.AddRange(
            new UserRole { UserId = ManagerId, RoleId = 2 },
            new UserRole { UserId = EmployeeId, RoleId = 3 },
            new UserRole { UserId = Employee2Id, RoleId = 3 });

        Db.ReminderTemplates.Add(new ReminderTemplate
        {
            Code = "DEFAULT_TR",
            Name = "Varsayılan",
            SubjectTemplate = ReminderService.DefaultSubjectTemplate,
            BodyTemplate = ReminderService.DefaultBodyTemplate,
            IsDefault = true,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        Db.SaveChanges();
    }

    /// <summary>Okunacak posta kutularını değiştirir (çoklu kutu testleri için).</summary>
    public void UseMailboxes(params string[] mailboxes) => GmailOptions.Mailboxes = mailboxes;

    public void ActAs(Guid userId, params string[] roles) =>
        (CurrentUser.UserId, CurrentUser.Roles) = (userId, roles.Length > 0 ? roles : [RoleCodes.Manager]);

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
