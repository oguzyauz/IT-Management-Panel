using System.Security.Claims;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.MemoryStorage;
using ItCockpit.Api.Auth;
using ItCockpit.Application.Abstractions;
using ItCockpit.Application.Contracts;
using ItCockpit.Application.Services;
using ItCockpit.Infrastructure;
using ItCockpit.Infrastructure.Jobs;
using ItCockpit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ItCockpit.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

builder.Services.AddInfrastructure(builder.Configuration);

// YENİ EKLEDİĞİMİZ SERVİS
builder.Services.AddHttpClient<IPublicHolidayService, PublicHolidayService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "ItCockpitApp/1.0");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "IT Manager Cockpit API", Version = "v1" });
    o.AddSecurityDefinition("MockUser", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = MockAuthenticationHandler.HeaderName,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Geliştirme kimliği: kullanıcı GUID'ini girin."
    });
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "MockUser"
                }
            },
            Array.Empty<string>()
        }
    });
});

// --- Kimlik doğrulama ---------------------------------------------------------------------------
var isGoogleAuth = string.Equals(authOptions.Provider, "Google", StringComparison.OrdinalIgnoreCase);
var isLocalAuth = string.Equals(authOptions.Provider, "Local", StringComparison.OrdinalIgnoreCase);
var isLdapAuth = string.Equals(authOptions.Provider, "Ldap", StringComparison.OrdinalIgnoreCase);

if (isLocalAuth || isLdapAuth)
{
    builder.Services.AddAuthentication(AuthSchemes.Local)
        .AddScheme<AuthenticationSchemeOptions, LocalAuthenticationHandler>(AuthSchemes.Local, _ => { });
}
else if (isGoogleAuth)
{
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = authOptions.GoogleAuthority;
            options.Audience = authOptions.GoogleClientId;
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var email = context.Principal?.FindFirstValue(ClaimTypes.Email)
                                ?? context.Principal?.FindFirstValue("email");

                    var domain = email?[(email.LastIndexOf('@') + 1)..];
                    var allowed = authOptions.AllowedDomains.Length == 0 ||
                                  authOptions.AllowedDomains.Any(d =>
                                      string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));

                    if (!allowed) context.Fail("Domain izinli değil.");
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    builder.Services.AddAuthentication(AuthSchemes.Mock)
        .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(AuthSchemes.Mock, _ => { });
}

builder.Services.AddAuthorization();

const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// --- Hangfire -----------------------------------------------------------------------------------
var hangfireConnection = builder.Configuration.GetConnectionString("Hangfire")
                         ?? builder.Configuration.GetConnectionString("Default");

builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings();

    if (builder.Configuration.GetValue("Hangfire:UseMemoryStorage", false) || string.IsNullOrWhiteSpace(hangfireConnection))
        config.UseMemoryStorage();
    else
        config.UseSqlServerStorage(hangfireConnection);
});

if (builder.Configuration.GetValue("Hangfire:EnableServer", true))
    builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseSerilogRequestLogging();

// --- Hata yönetimi ------------------------------------------------------------------------------
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = feature?.Error;

    var (status, code, title) = exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", "Kayıt bulunamadı"),
        DomainRuleException dre => (StatusCodes.Status400BadRequest, dre.Code, "İş kuralı ihlali"),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "FORBIDDEN", "Yetkisiz işlem"),
        _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Beklenmeyen hata")
    };

    if (status == StatusCodes.Status500InternalServerError)
        Log.Error(exception, "İşlenmemiş hata");

    var problem = new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = exception?.Message,
        Type = code
    };

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(problem);
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
});

// --- Başlangıç işleri ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        var db = sp.GetRequiredService<AppDbContext>();

        if (db.Database.IsSqlite())
            await db.Database.EnsureCreatedAsync();
        else
            await db.Database.MigrateAsync();
    }

    if (app.Configuration.GetValue("Database:SeedOnStartup", true))
        await sp.GetRequiredService<DatabaseSeeder>().SeedAsync();

    if (app.Configuration.GetValue("Hangfire:EnableServer", true))
    {
        var settings = sp.GetRequiredService<IAppSettingsProvider>();
        var interval = await settings.GetIntAsync(
            ItCockpit.Domain.Entities.AppSettingKeys.GmailPollIntervalMinutes, 5);

        sp.GetRequiredService<IRecurringJobManager>().AddOrUpdate<GmailIngestionJob>(
            GmailIngestionJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            $"*/{Math.Clamp(interval, 1, 59)} * * * *");

        sp.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ArchiveTicketsJob>(
            ArchiveTicketsJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            "0 2 * * *");
    }
}

app.Run();

public partial class Program;