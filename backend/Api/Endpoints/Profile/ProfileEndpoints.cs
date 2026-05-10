using Microsoft.EntityFrameworkCore;
using Synapse.Core.Services.Security;
using Synapse.Infrastructure.Data;

namespace Synapse.Api.Endpoints.Profile;

public static class ProfileEndpoints
{
    public static void MapProfileEmbeddingEndpoints(this WebApplication app)
    {
        // Internal endpoint called by Python profiler agent after embedding computation.
        // Stores AES-256-GCM encrypted copy — encryption key never leaves .NET process.
        app.MapPost("/api/internal/profile/embedding", async (
            EmbeddingStoreRequest req,
            IEmbeddingEncryptionService encSvc,
            SynapseDbContext db,
            IConfiguration config,
            HttpRequest httpReq,
            CancellationToken ct) =>
        {
            var secret = Environment.GetEnvironmentVariable("INTERNAL_API_SECRET")
                ?? config["InternalApiSecret"];
            if (!string.IsNullOrEmpty(secret) &&
                httpReq.Headers["X-Internal-Secret"].FirstOrDefault() != secret)
                return Results.Unauthorized();

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == req.UserId, ct);
            if (profile is null) return Results.NotFound();

            profile.EncryptedEmbedding = encSvc.Encrypt(req.Embedding.ToArray());
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).AllowAnonymous();
    }
}

public record EmbeddingStoreRequest(int UserId, List<float> Embedding);
