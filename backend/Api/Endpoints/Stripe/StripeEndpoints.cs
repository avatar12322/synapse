using System.Security.Claims;
using Synapse.Core.Services.Business;
using Synapse.Core.Services.Stripe;

namespace Synapse.Api.Endpoints.Stripe;

public static class StripeEndpoints
{
    public static void MapStripeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stripe");

        // POST /api/stripe/connect/onboard — get Stripe Connect onboarding URL
        group.MapPost("/connect/onboard", async (
            ClaimsPrincipal user,
            IStripeService stripeSvc,
            IBusinessService businessSvc,
            HttpRequest request,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null)
                return Results.BadRequest(new { error = "Create a business profile first." });

            var baseUrl = $"{request.Scheme}://{request.Host}";
            var returnUrl = $"{baseUrl}/business/stripe/return";
            var refreshUrl = $"{baseUrl}/business/stripe/refresh";

            var url = await stripeSvc.CreateConnectOnboardingLinkAsync(
                business.Id, returnUrl, refreshUrl, ct);

            return Results.Ok(new { url });
        }).RequireAuthorization();

        // POST /api/stripe/connect/complete?accountId=...
        group.MapPost("/connect/complete", async (
            string accountId,
            ClaimsPrincipal user,
            IStripeService stripeSvc,
            IBusinessService businessSvc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null)
                return Results.BadRequest();

            var success = await stripeSvc.CompleteOnboardingAsync(business.Id, accountId, ct);
            return Results.Ok(new { chargesEnabled = success });
        }).RequireAuthorization();

        // POST /api/stripe/webhook — public, raw body required for signature verification
        app.MapPost("/api/stripe/webhook", async (
            HttpRequest request,
            IStripeService stripeSvc,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
                ?? config["Stripe:WebhookSecret"]
                ?? string.Empty;

            if (string.IsNullOrEmpty(webhookSecret))
                return Results.Problem("Stripe webhook secret not configured.", statusCode: 500);

            string json;
            using (var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
                json = await reader.ReadToEndAsync(ct);

            var signature = request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
            var handled = await stripeSvc.HandleWebhookAsync(json, signature, webhookSecret, ct);

            return handled ? Results.Ok() : Results.BadRequest(new { error = "Invalid signature or unknown event" });
        }).AllowAnonymous();
    }
}
