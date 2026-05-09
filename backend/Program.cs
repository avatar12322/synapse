using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using System.Text;
using Synapse.Infrastructure.Data;
using Synapse.Api.Endpoints.Auth;
using Synapse.Api.Endpoints.Business;
using Synapse.Api.Endpoints.Match;
using Synapse.Api.Endpoints.Mission;
using Synapse.Api.Endpoints.Notification;
using Synapse.Api.Endpoints.Presence;
using Synapse.Api.Endpoints.Stripe;
using Synapse.Core.Services.Auth;
using Synapse.Core.Services.Business;
using Synapse.Core.Services.Match;
using Synapse.Core.Services.Mission;
using Synapse.Core.Services.Notification;
using Synapse.Core.Services.Stripe;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// Redis or in-memory fallback
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
if (!string.IsNullOrEmpty(redisConnection))
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);
else
    builder.Services.AddDistributedMemoryCache();

// Database – Railway PostgreSQL URL format or appsettings fallback
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(connectionString))
{
    // removed
    // removed
    // removed
    connectionString = "REMOVED_IN_HISTORY_CLEANUP";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<SynapseDbContext>(o =>
{
    o.UseNpgsql(dataSource, npg =>
    {
        npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        npg.UseNetTopologySuite();
        npg.UseVector();
    });
    o.EnableDetailedErrors(builder.Environment.IsDevelopment());
});

// NetTopologySuite geometry factory (SRID 4326, WGS84)
builder.Services.AddSingleton(NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326));

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "synapse-super-secret-jwt-key-min-256-bits!!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "synapse",
            ValidAudience = "synapse-users",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue("access_token", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:3000"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()
    .WithExposedHeaders("Content-Disposition")));

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IStripeService, StripeService>();

// Background services
builder.Services.AddHostedService<NotificationCleanupService>();
builder.Services.AddHostedService<MissionExpiryService>();

// Stripe global API key
StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")
    ?? builder.Configuration["Stripe:SecretKey"]
    ?? string.Empty;

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

var appPort = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{appPort}");

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.UseResponseCompression();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "healthy", app = "Synapse", version = "1.0.0", timestamp = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapAuthEndpoints();
app.MapNotificationEndpoints();
app.MapBusinessEndpoints();
app.MapMissionEndpoints();
app.MapMatchEndpoints();
app.MapStripeEndpoints();
app.MapPresenceEndpoints();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SynapseDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var retries = 0;
    while (retries < 3)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrated");
            break;
        }
        catch (Exception ex)
        {
            retries++;
            logger.LogError(ex, "Migration attempt {Attempt} failed", retries);
            if (retries >= 3) throw;
            await Task.Delay(5000);
        }
    }
}

Console.WriteLine("=== SYNAPSE BACKEND READY (Phase 1) ===");
app.Run();

public class NotificationCleanupService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<NotificationCleanupService> _logger;

    public NotificationCleanupService(IServiceProvider sp, ILogger<NotificationCleanupService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SynapseDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var old = db.Notifications.Where(n => n.CreatedAt < cutoff && n.IsRead).Take(1000);
                db.Notifications.RemoveRange(old);
                var count = await db.SaveChangesAsync(ct);
                if (count > 0) _logger.LogInformation("Cleaned {Count} old notifications", count);
            }
            catch (Exception ex) { _logger.LogError(ex, "Notification cleanup error"); }
            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }
}

public class MissionExpiryService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<MissionExpiryService> _logger;

    public MissionExpiryService(IServiceProvider sp, ILogger<MissionExpiryService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IMissionService>();
                await svc.ExpireOldMissionsAsync(ct);
            }
            catch (Exception ex) { _logger.LogError(ex, "Mission expiry error"); }
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
