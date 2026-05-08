using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Synapse.Infrastructure.Data;
using Synapse.Api.Endpoints.Auth;
using Synapse.Api.Endpoints.Notification;
using Synapse.Core.Services.Auth;
using Synapse.Core.Services.Notification;

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

builder.Services.AddDbContext<SynapseDbContext>(o =>
{
    o.UseNpgsql(connectionString, npg =>
        npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
    o.EnableDetailedErrors(builder.Environment.IsDevelopment());
});

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

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

var appPort = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{appPort}");

app.UseResponseCompression();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { status = "healthy", app = "Synapse", version = "0.1.0", timestamp = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapAuthEndpoints();
app.MapNotificationEndpoints();

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

Console.WriteLine("=== SYNAPSE BACKEND READY ===");
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
