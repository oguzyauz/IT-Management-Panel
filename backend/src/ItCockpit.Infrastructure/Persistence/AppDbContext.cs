using ItCockpit.Application.Abstractions;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ItCockpit.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketAssignment> TicketAssignments => Set<TicketAssignment>();
    public DbSet<TicketStatusHistory> TicketStatusHistory => Set<TicketStatusHistory>();
    public DbSet<TicketNote> TicketNotes => Set<TicketNote>();
    public DbSet<TicketMailSource> TicketMailSources => Set<TicketMailSource>();
    public DbSet<TicketParseWarning> TicketParseWarnings => Set<TicketParseWarning>();

    public DbSet<WorkScheduleWeek> WorkScheduleWeeks => Set<WorkScheduleWeek>();
    public DbSet<WorkScheduleDay> WorkScheduleDays => Set<WorkScheduleDay>();
    public DbSet<WorkScheduleApproval> WorkScheduleApprovals => Set<WorkScheduleApproval>();
    public DbSet<WorkCalendarEntry> WorkCalendar => Set<WorkCalendarEntry>();

    public DbSet<ReminderTemplate> ReminderTemplates => Set<ReminderTemplate>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<GmailSyncState> GmailSyncStates => Set<GmailSyncState>();

    // IAppDbContext.SaveChangesAsync, DbContext'in kendi implementasyonu ile karşılanır.

    /// <summary>
    /// SQL Server <c>datetime2</c> kolonları <see cref="DateTimeKind"/> bilgisini saklamaz; okunan
    /// değerler <c>Unspecified</c> olarak döner. Bu durumda API çıktısındaki tarihler <c>Z</c> soneki
    /// olmadan serileştirilir ve istemci bunları yerel saat sanar. Tüm DateTime alanları okunurken
    /// açıkça UTC olarak işaretlenir.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(UtcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(NullableUtcConverter);
            }
        }

        base.OnModelCreating(b);
    }
}
