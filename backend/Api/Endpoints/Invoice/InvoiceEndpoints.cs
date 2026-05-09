using System.Security.Claims;
using Synapse.Core.DTOs.Invoice;
using Synapse.Core.Models.Invoice;
using Synapse.Core.Services.Business;
using Synapse.Core.Services.Invoice;

namespace Synapse.Api.Endpoints.Invoice;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invoices").RequireAuthorization();

        // POST /api/invoices/generate — trigger invoice generation for a period
        group.MapPost("/generate", async (
            InvoiceRequestDto req,
            ClaimsPrincipal user,
            IInvoiceAggregatorService invoiceSvc,
            IBusinessService businessSvc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null)
                return Results.BadRequest(new { error = "No business profile found" });

            // Only business owner or admin can generate invoice for their business
            if (business.Id != req.BusinessId)
                return Results.Forbid();

            if (req.PeriodEnd < req.PeriodStart)
                return Results.BadRequest(new { error = "PeriodEnd must be after PeriodStart" });

            var invoice = await invoiceSvc.CreateInvoiceAsync(req.BusinessId, req.PeriodStart, req.PeriodEnd, ct);
            return invoice is null
                ? Results.NotFound()
                : Results.Ok(invoice);
        });

        // GET /api/invoices/{id} — get invoice status
        group.MapGet("/{id:guid}", async (
            Guid id,
            IInvoiceAggregatorService invoiceSvc,
            CancellationToken ct) =>
        {
            var invoice = await invoiceSvc.GetInvoiceStatusAsync(id, ct);
            return invoice is null ? Results.NotFound() : Results.Ok(invoice);
        });

        // GET /api/invoices/business/{businessId} — list invoices for a business
        group.MapGet("/business/{businessId:int}", async (
            int businessId,
            ClaimsPrincipal user,
            IInvoiceAggregatorService invoiceSvc,
            IBusinessService businessSvc,
            CancellationToken ct) =>
        {
            var ownerId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var business = await businessSvc.GetByOwnerAsync(ownerId, ct);
            if (business is null || business.Id != businessId)
                return Results.Forbid();

            var invoices = await invoiceSvc.GetBusinessInvoicesAsync(businessId, ct);
            return Results.Ok(invoices);
        });

        // POST /api/invoices/{id}/status — called by KSeF service to update status
        app.MapPost("/api/invoices/{id:guid}/status", async (
            Guid id,
            InvoiceStatusUpdateRequest req,
            IInvoiceAggregatorService invoiceSvc,
            IConfiguration config,
            HttpRequest request,
            CancellationToken ct) =>
        {
            // Simple shared-secret auth for service-to-service calls
            var internalSecret = Environment.GetEnvironmentVariable("INTERNAL_API_SECRET")
                ?? config["InternalApiSecret"] ?? string.Empty;

            if (!string.IsNullOrEmpty(internalSecret))
            {
                var provided = request.Headers["X-Internal-Secret"].FirstOrDefault() ?? string.Empty;
                if (provided != internalSecret)
                    return Results.Unauthorized();
            }

            var updated = await invoiceSvc.UpdateInvoiceStatusAsync(
                id, req.Status, req.ReferenceNumber, req.ErrorMessage, ct);

            return updated ? Results.Ok() : Results.NotFound();
        }).AllowAnonymous();
    }
}

public record InvoiceStatusUpdateRequest(KsefInvoiceStatus Status, string? ReferenceNumber, string? ErrorMessage);
