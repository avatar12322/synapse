using Microsoft.EntityFrameworkCore;
using Synapse.Infrastructure.Data;
using Synapse.Core.DTOs.Auth;
using Synapse.Core.Services.Auth;
using Synapse.Core.Models.User;
using System.Security.Claims;

namespace Synapse.Api.Endpoints.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            SynapseDbContext db,
            IAuthService authService,
            HttpContext httpContext,
            IHostEnvironment env) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest("All fields are required");

            if (request.Password.Length < 6)
                return Results.BadRequest("Password must be at least 6 characters");

            var existing = await db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.Username);
            if (existing != null)
                return Results.BadRequest("User with this email or username already exists");

            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordHash = authService.HashPassword(request.Password),
                LastLoginAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var refreshToken = authService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync();

            var accessToken = authService.GenerateJwtToken(user);
            SetAuthCookies(httpContext, accessToken, refreshToken, env.IsProduction());

            return Results.Ok(new AuthCookieResponse(
                new UserDto(user.Id, user.Email, user.Username, user.Language, user.Role.ToString())));
        });

        group.MapPost("/login", async (
            LoginRequest request,
            SynapseDbContext db,
            IAuthService authService,
            HttpContext httpContext,
            IHostEnvironment env) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest("Email and password are required");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !authService.VerifyPassword(request.Password, user.PasswordHash))
                return Results.BadRequest("Invalid email or password");

            var refreshToken = authService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var accessToken = authService.GenerateJwtToken(user);
            SetAuthCookies(httpContext, accessToken, refreshToken, env.IsProduction());

            return Results.Ok(new AuthCookieResponse(
                new UserDto(user.Id, user.Email, user.Username, user.Language, user.Role.ToString())));
        });

        group.MapPost("/refresh", async (
            SynapseDbContext db,
            IAuthService authService,
            HttpContext httpContext,
            IHostEnvironment env) =>
        {
            var incomingToken = httpContext.Request.Cookies["refresh_token"];
            if (string.IsNullOrEmpty(incomingToken))
                return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == incomingToken);
            if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
                return Results.Unauthorized();

            var newRefreshToken = authService.GenerateRefreshToken();
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await db.SaveChangesAsync();

            var newAccessToken = authService.GenerateJwtToken(user);
            SetAuthCookies(httpContext, newAccessToken, newRefreshToken, env.IsProduction());

            return Results.Ok(new { message = "Token refreshed" });
        });

        group.MapPost("/logout", (HttpContext httpContext, IHostEnvironment env) =>
        {
            ClearAuthCookies(httpContext, env.IsProduction());
            return Results.Ok(new { message = "Logged out" });
        });

        group.MapGet("/me", async (SynapseDbContext db, ClaimsPrincipal user) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Results.Unauthorized();

            var currentUser = await db.Users.FindAsync(int.Parse(userIdClaim.Value));
            if (currentUser == null) return Results.NotFound();

            return Results.Ok(new UserDto(
                currentUser.Id, currentUser.Email, currentUser.Username,
                currentUser.Language, currentUser.Role.ToString()));
        }).RequireAuthorization();

        group.MapPut("/language", async (
            SynapseDbContext db,
            ClaimsPrincipal user,
            HttpContext httpContext) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Results.Unauthorized();

            var body = await httpContext.Request.ReadFromJsonAsync<LanguageRequest>();
            if (body == null) return Results.BadRequest();

            var currentUser = await db.Users.FindAsync(int.Parse(userIdClaim.Value));
            if (currentUser == null) return Results.NotFound();

            currentUser.Language = body.Language;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }

    private static void SetAuthCookies(HttpContext ctx, string accessToken, string refreshToken, bool isProduction)
    {
        var sameSite = isProduction ? SameSiteMode.None : SameSiteMode.Lax;

        ctx.Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = sameSite,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        });

        ctx.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = sameSite,
            Path = "/api/auth/refresh",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private static void ClearAuthCookies(HttpContext ctx, bool isProduction)
    {
        var sameSite = isProduction ? SameSiteMode.None : SameSiteMode.Lax;

        ctx.Response.Cookies.Append("access_token", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = sameSite,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch
        });

        ctx.Response.Cookies.Append("refresh_token", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = sameSite,
            Path = "/api/auth/refresh",
            Expires = DateTimeOffset.UnixEpoch
        });
    }
}

public record LanguageRequest(string Language);
