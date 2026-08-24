using ItCockpit.Domain;
using ItCockpit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItCockpit.Infrastructure.Persistence;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("Teams");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.ManagerUser)
            .WithMany()
            .HasForeignKey(x => x.ManagerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200);
        b.HasIndex(x => x.Email).IsUnique();

        // Parolanın kendisi değil, PBKDF2 özeti saklanır (bkz. PasswordHasher).
        b.Property(x => x.PasswordHash).HasMaxLength(400);

        // Users üzerinde global query filter yoktur (bkz. User.IsActive açıklaması).

        b.HasOne(x => x.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.Id);

        // Sabit lookup tablosu: kimlikler ortamlar arasında aynı kalsın diye identity kullanılmaz.
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("UserSessions");
        b.HasKey(x => x.Id);

        // Token'ın SHA-256 özeti; base64 44 karakter.
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();

        b.HasIndex(x => x.ExpiresAtUtc);

        // Kullanıcı silinmez ama silinseydi oturumun ayakta kalmasının anlamı olmazdı.
        b.HasOne(x => x.User).WithMany(u => u.Sessions)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles");
        b.HasKey(x => new { x.UserId, x.RoleId });

        b.HasOne(x => x.User).WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Role).WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("Tickets");
        b.HasKey(x => x.Id);

        b.Property(x => x.ExternalTicketNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.TicketType).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.RequesterName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ApplicationName).HasMaxLength(200).IsRequired();
        b.Property(x => x.CategoryPath).HasMaxLength(500);
        b.Property(x => x.ExternalReference).HasMaxLength(200);
        b.Property(x => x.SourceRequestId).HasMaxLength(50);
        b.Property(x => x.ExternalUrl).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        b.HasIndex(x => x.ExternalTicketNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Status, x.OriginalSentAtUtc });
        b.HasIndex(x => new { x.AssigneeUserId, x.Status });
        b.HasIndex(x => x.SourceRequestId).HasFilter("[SourceRequestId] IS NOT NULL");
        b.HasIndex(x => x.UpdatedAtUtc);

        b.HasQueryFilter(x => !x.IsDeleted);

        b.HasOne(x => x.AssigneeUser)
            .WithMany()
            .HasForeignKey(x => x.AssigneeUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> b)
    {
        b.ToTable("TicketAssignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.TicketId, x.AssignedAtUtc });

        // Ticket'ın soft-delete filtresiyle eşleşen filtre — aksi hâlde silinmiş ticket'ın
        // bağımlı kayıtları zorunlu navigasyon üzerinden beklenmedik sonuç üretir.
        b.HasQueryFilter(x => !x.Ticket.IsDeleted);

        b.HasOne(x => x.Ticket).WithMany(t => t.Assignments)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AssignedToUser).WithMany()
            .HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.NoAction);

        // Sistem tarafından yapılan otomatik atamalarda AssignedByUserId null'dır.
        b.HasOne(x => x.AssignedByUser).WithMany()
            .HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> b)
    {
        b.ToTable("TicketStatusHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.TicketId, x.ChangedAtUtc });
        b.HasQueryFilter(x => !x.Ticket.IsDeleted);

        b.HasOne(x => x.Ticket).WithMany(t => t.StatusHistory)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        // Sistem kaynaklı geçişlerde ChangedByUserId null'dır.
        b.HasOne(x => x.ChangedByUser).WithMany()
            .HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class TicketNoteConfiguration : IEntityTypeConfiguration<TicketNote>
{
    public void Configure(EntityTypeBuilder<TicketNote> b)
    {
        b.ToTable("TicketNotes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Body).IsRequired();
        b.HasQueryFilter(x => !x.IsDeleted && !x.Ticket.IsDeleted);
        b.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });

        b.HasOne(x => x.Ticket).WithMany(t => t.Notes)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AuthorUser).WithMany()
            .HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class TicketMailSourceConfiguration : IEntityTypeConfiguration<TicketMailSource>
{
    public void Configure(EntityTypeBuilder<TicketMailSource> b)
    {
        b.ToTable("TicketMailSources");
        b.HasKey(x => x.Id);

        b.Property(x => x.SourceMailbox).HasMaxLength(320).IsRequired();
        b.Property(x => x.GmailMessageId).HasMaxLength(100).IsRequired();
        b.Property(x => x.GmailThreadId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalSender).HasMaxLength(320).IsRequired();
        b.Property(x => x.ForwardedBy).HasMaxLength(320);

        b.HasIndex(x => x.GmailMessageId).IsUnique();
        b.HasIndex(x => x.GmailThreadId);
        b.HasIndex(x => x.SourceMailbox);
        b.HasIndex(x => new { x.Subject, x.OriginalSentAtUtc });
        b.HasQueryFilter(x => !x.Ticket.IsDeleted);

        b.HasOne(x => x.Ticket).WithMany(t => t.MailSources)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TicketParseWarningConfiguration : IEntityTypeConfiguration<TicketParseWarning>
{
    public void Configure(EntityTypeBuilder<TicketParseWarning> b)
    {
        b.ToTable("TicketParseWarnings");
        b.HasKey(x => x.Id);

        b.Property(x => x.GmailMessageId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.FieldName).HasMaxLength(100);
        b.Property(x => x.SubjectValue).HasMaxLength(500);
        b.Property(x => x.BodyValue).HasMaxLength(500);

        b.HasIndex(x => new { x.IsAcknowledged, x.Severity });

        // Reddedilen maillerin uyarısında TicketId null'dır ve görünür kalmalıdır.
        b.HasQueryFilter(x => x.Ticket == null || !x.Ticket.IsDeleted);

        b.HasOne(x => x.Ticket).WithMany(t => t.ParseWarnings)
            .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkScheduleWeekConfiguration : IEntityTypeConfiguration<WorkScheduleWeek>
{
    public void Configure(EntityTypeBuilder<WorkScheduleWeek> b)
    {
        b.ToTable("WorkScheduleWeeks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.RuleViolationNote).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
        b.HasIndex(x => x.WeekStartDate);

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkScheduleDayConfiguration : IEntityTypeConfiguration<WorkScheduleDay>
{
    public void Configure(EntityTypeBuilder<WorkScheduleDay> b)
    {
        b.ToTable("WorkScheduleDays");
        b.HasKey(x => x.Id);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.OverrideNote).HasMaxLength(500);
        b.HasIndex(x => new { x.WorkScheduleWeekId, x.Date }).IsUnique();
        b.HasIndex(x => new { x.Date, x.Mode });

        b.HasOne(x => x.Week).WithMany(w => w.Days)
            .HasForeignKey(x => x.WorkScheduleWeekId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkScheduleApprovalConfiguration : IEntityTypeConfiguration<WorkScheduleApproval>
{
    public void Configure(EntityTypeBuilder<WorkScheduleApproval> b)
    {
        b.ToTable("WorkScheduleApprovals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);

        b.HasOne(x => x.Week).WithMany(w => w.Approvals)
            .HasForeignKey(x => x.WorkScheduleWeekId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendarEntry>
{
    public void Configure(EntityTypeBuilder<WorkCalendarEntry> b)
    {
        b.ToTable("WorkCalendar");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Date).IsUnique();
        b.HasIndex(x => x.Year);
    }
}

public sealed class ReminderTemplateConfiguration : IEntityTypeConfiguration<ReminderTemplate>
{
    public void Configure(EntityTypeBuilder<ReminderTemplate> b)
    {
        b.ToTable("ReminderTemplates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SubjectTemplate).HasMaxLength(500).IsRequired();
        b.Property(x => x.BodyTemplate).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class ReminderDeliveryConfiguration : IEntityTypeConfiguration<ReminderDelivery>
{
    public void Configure(EntityTypeBuilder<ReminderDelivery> b)
    {
        b.ToTable("ReminderDeliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        b.Property(x => x.Body).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.HasIndex(x => new { x.RecipientUserId, x.CreatedAtUtc });
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.RecipientUser).WithMany()
            .HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.SentByUser).WithMany()
            .HasForeignKey(x => x.SentByUserId).OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Template).WithMany()
            .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("LeaveRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);

        b.HasIndex(x => new { x.UserId, x.StartDate });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.StartDate, x.EndDate });

        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ReviewedByUser).WithMany()
            .HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorEmail).HasMaxLength(256);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.OccurredAtUtc);
    }
}

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSettings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(200);
        b.Property(x => x.Value).IsRequired();
        b.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Category).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
    }
}

public sealed class GmailSyncStateConfiguration : IEntityTypeConfiguration<GmailSyncState>
{
    public void Configure(EntityTypeBuilder<GmailSyncState> b)
    {
        b.ToTable("GmailSyncStates");
        b.HasKey(x => x.Id);
        b.Property(x => x.MailboxAddress).HasMaxLength(320).IsRequired();
        b.Property(x => x.LastHistoryId).HasMaxLength(50);
        b.Property(x => x.LastSyncStatus).HasMaxLength(20);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasIndex(x => x.MailboxAddress).IsUnique();
    }
}
