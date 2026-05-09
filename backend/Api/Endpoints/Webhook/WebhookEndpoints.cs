using System.Security.Claims;
using Synapse.Core.Services.Business;
using Synapse.Core.Services.Webhook;

namespace Synapse.Api.Endpoints.Webhook;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        // POST /api/webhooks/pos — public endpoint, security via HMAC signature
        app.MapPost("/api/webhooks/pos", async (
            HttpRequest request,
            IPosWebhookService webhookSvc,
            CancellationToken ct) =>
        {
            string rawBody;
            using (var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
                rawBody = await reader.ReadToEndAsync(ct);

            var signature = request.Headers["X-Synapse-Signature"].FirstOrDefault() ?? string.Empty;

            // businessId comes from the JSON body; we need it to look up the per-business secret
            string? businessId = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                doc.RootElement.TryGetProperty("businessId", out var bizProp);
                businessId = bizProp.GetString();
            }
            catch { }

            if (string.IsNullOrEmpty(businessId))
                return Results.BadRequest(new { error = "Missing businessId in payload" });

            if (string.IsNullOrEmpty(signature))
                return Results.BadRequest(new { error = "Missing X-Synapse-Signature header" });

            var (success, missionId, error) = await webhookSvc.ProcessAsync(businessId, rawBody, signature, ct);

            return success
                ? Results.Ok(new { success = true, missionId })
                : Results.BadRequest(new { success = false, error });
        }).AllowAnonymous();

        var secrets = app.MapGroup("/api/businesses").RequireAuthorization();

        // POST /api/businesses/webhook-secret — generate POS webhook HMAC key
        secrets.MapPost("/webhook-secret", async (
            ClaimsPrincipal user,
            IBusinessService businessSvc,
            IPosWebhookService webhookSvc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null)
                return Results.BadRequest(new { error = "No business profile found" });

            var secret = await webhookSvc.GeneratePosSecretAsync(business.Id, ct);
            return Results.Ok(new { secret, secretType = "pos-webhook" });
        });

        // POST /api/businesses/nfc-secret — generate NFC NDEF HMAC key
        secrets.MapPost("/nfc-secret", async (
            ClaimsPrincipal user,
            IBusinessService businessSvc,
            IPosWebhookService webhookSvc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null)
                return Results.BadRequest(new { error = "No business profile found" });

            var secret = await webhookSvc.GenerateNfcSecretAsync(business.Id, ct);
            return Results.Ok(new { secret, secretType = "nfc" });
        });
    }
}
