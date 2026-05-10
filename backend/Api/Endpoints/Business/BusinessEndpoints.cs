using System.Security.Claims;
using Synapse.Core.DTOs.Business;
using Synapse.Core.Services.Business;
using Synapse.Infrastructure.Data;
using Synapse.Core.Models.User;
using Synapse.Core.Models.Business;
using NetTopologySuite.Geometries;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.Text;

namespace Synapse.Api.Endpoints.Business;

public static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/businesses");

        // Public: nearby businesses
        group.MapGet("/nearby", async (
            double lat, double lng, int radius = 2000, string? category = null,
            IBusinessService svc = default!, CancellationToken ct = default) =>
        {
            var results = await svc.GetNearbyAsync(lat, lng, radius, category, ct);
            return Results.Ok(results);
        });

        // Public: get single business
        group.MapGet("/{id:int}", async (int id, IBusinessService svc, CancellationToken ct) =>
        {
            var b = await svc.GetByIdAsync(id, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        });

        // Auth: create business (Role=Business)
        group.MapPost("/", async (
            CreateBusinessRequest req,
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.CreateAsync(ownerId, req, ct);
            return Results.Created($"/api/businesses/{b.Id}", b);
        }).RequireAuthorization();

        // Auth: update own business
        group.MapPut("/{id:int}", async (
            int id,
            UpdateBusinessRequest req,
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.UpdateAsync(id, ownerId, req, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).RequireAuthorization();

        // Auth: get my business
        group.MapGet("/mine", async (
            ClaimsPrincipal user,
            IBusinessService svc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var b = await svc.GetByOwnerAsync(ownerId, ct);
            return b is null ? Results.NotFound() : Results.Ok(b);
        }).RequireAuthorization();

        // DEV ONLY: Seed data (Forceful)
        group.MapGet("/seed", async (SynapseDbContext db, GeometryFactory gf) =>
        {
            var pass = BCrypt.Net.BCrypt.HashPassword("Password123!");
            
            // 1. Upsert Users
            var emails = new[] { "business@example.com", "user_a@example.com", "user_b@example.com" };
            var existingUsers = await db.Users.Where(u => emails.Contains(u.Email)).ToListAsync();

            async Task<User> UpsertUser(string email, string username, UserRole role) {
                var u = existingUsers.FirstOrDefault(x => x.Email == email);
                if (u == null) {
                    u = new User { Email = email, Username = username, PasswordHash = pass, Role = role };
                    db.Users.Add(u);
                } else {
                    u.PasswordHash = pass;
                    u.Username = username;
                    u.Role = role;
                }
                return u;
            }

            var owner = await UpsertUser("business@example.com", "krakow_cafe", UserRole.Business);
            var userA = await UpsertUser("user_a@example.com", "tester_a", UserRole.User);
            var userB = await UpsertUser("user_b@example.com", "tester_b", UserRole.User);
            
            await db.SaveChangesAsync();

            // 2. Upsert Businesses by name
            var venueNames = new[] { "Propaganda Pub", "Camelot Cafe", "Forum Przestrzenie" };
            var existingVenues = await db.Businesses.Where(b => venueNames.Contains(b.Name)).ToListAsync();

            void UpsertVenue(string name, string address, string category, Coordinate coord) {
                var b = existingVenues.FirstOrDefault(x => x.Name == name);
                if (b == null) {
                    var location = gf.CreatePoint(coord);
                    location.SRID = 4326;
                    db.Businesses.Add(new Synapse.Core.Models.Business.Business {
                        Name = name, Address = address, City = "Kraków", Category = category,
                        Location = location, OwnerId = owner.Id, IsActive = true,
                        NfcSecret = Convert.ToBase64String(Encoding.UTF8.GetBytes("test_nfc_secret_1234567890123456"))
                    });
                } else {
                    b.IsActive = true;
                    b.OwnerId = owner.Id;
                }
            }

            UpsertVenue("Propaganda Pub",  "Miodowa 20",               "Pub",      new Coordinate(19.9449, 50.0519));
            UpsertVenue("Camelot Cafe",    "Świętego Tomasza 17",      "Coffee",   new Coordinate(19.9395, 50.0628));
            UpsertVenue("Forum Przestrzenie", "Marii Konopnickiej 28", "Cultural", new Coordinate(19.9365, 50.0460));
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Seeded/Updated 3 users and ensured venues exist.", credentials = "Password123!" });
        });
    }
}
