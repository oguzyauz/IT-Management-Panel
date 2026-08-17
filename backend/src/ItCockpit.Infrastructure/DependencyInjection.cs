using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Parsing;
using ItCockpit.Application.Services;
using ItCockpit.Infrastructure.Gmail;
using ItCockpit.Infrastructure.Jobs;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItCockpit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<GmailIngestionOptions>(config.GetSection(GmailIngestionOptions.SectionName));
        services.Configure<TicketMailParserOptions>(config.GetSection(TicketMailParserOptions.SectionName));
        services.Configure<MockGmailOptions>(config.GetSection(MockGmailOptions.SectionName));
        services.Configure<ReminderOptions>(config.GetSection(ReminderOptions.SectionName));
        services.Configure<Services.AppClockOptions>(config.GetSection(Services.AppClockOptions.SectionName));

        // Sağlayıcı seçimi:
        //   SqlServer — kurumsal kullanım; migration'larla yönetilir.
        //   Sqlite    — tek makinelik taşınabilir kurulum; SQL Server kurmayı gerektirmez.
        //               Şema EnsureCreated ile oluşturulur, migration kullanılmaz (bkz. README).
        var dbProvider = config["Database:Provider"] ?? "SqlServer";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(config.GetConnectionString("Sqlite") ?? "Data Source=it-cockpit.db");
            else
                options.UseSqlServer(
                    config.GetConnectionString("Default"),
                    sql => sql.EnableRetryOnFailure(3));
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddMemoryCache();
        services.AddSingleton<IClock, Services.SystemClock>();
        services.AddScoped<IAppSettingsProvider, Services.AppSettingsProvider>();
        services.AddScoped<IAuditLogger, Services.AuditLogger>();

        // Sağlayıcı seçimi: geliştirmede Mock, üretimde Google.
        var gmailProvider = config[$"{GmailIngestionOptions.SectionName}:Provider"] ?? "Mock";
        if (string.Equals(gmailProvider, "Google", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IGmailTicketSource, GmailTicketSource>();
        else
            services.AddScoped<IGmailTicketSource, MockGmailTicketSource>();

        var reminderProvider = config[$"{ReminderOptions.SectionName}:Provider"] ?? "Mock";
        if (string.Equals(reminderProvider, "Google", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IReminderMailSender, GmailReminderMailSender>();
        else
            services.AddScoped<IReminderMailSender, MockReminderMailSender>();

        services.AddSingleton<TicketMailParser>();
        services.AddSingleton<GmailCredentialsInspector>();

        services.AddScoped<AccountService>();
        services.AddScoped<MailboxRegistry>();
        services.AddScoped<TicketIngestionService>();
        services.AddScoped<TicketService>();
        services.AddScoped<WorkScheduleService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<DashboardService>();

        services.AddScoped<GmailIngestionJob>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
