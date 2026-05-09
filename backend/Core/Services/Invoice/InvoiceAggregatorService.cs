using Microsoft.EntityFrameworkCore;
using Synapse.Core.DTOs.Invoice;
using Synapse.Core.Models.Invoice;
using Synapse.Core.Models.Mission;
using Synapse.Infrastructure.Data;

namespace Synapse.Core.Services.Invoice;

public interface IInvoiceAggregatorService
{
    Task<InvoiceStatusDto?> CreateInvoiceAsync(int businessId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default);
    Task<InvoiceStatusDto?> GetInvoiceStatusAsync(Guid invoiceId, CancellationToken ct = default);
    Task<List<InvoiceStatusDto>> GetBusinessInvoicesAsync(int businessId, CancellationToken ct = default);
    Task<bool> UpdateInvoiceStatusAsync(Guid invoiceId, KsefInvoiceStatus status, string? referenceNumber, string? error, CancellationToken ct = default);
}

public class InvoiceAggregatorService(SynapseDbContext db, IConfiguration config) : IInvoiceAggregatorService
{
    public async Task<InvoiceStatusDto?> CreateInvoiceAsync(
        int businessId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var business = await db.Businesses.FindAsync([businessId], ct);
        if (business is null) return null;

        var periodStartDt = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndDt = periodEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var missions = await db.Missions
            .Where(m =>
                m.BusinessId == businessId &&
                m.Status == MissionStatus.Completed &&
                m.CompletedAt >= periodStartDt &&
                m.CompletedAt <= periodEndDt &&
                m.CommissionAmountCents > 0)
            .ToListAsync(ct);

        var totalCents = missions.Sum(m => m.CommissionAmountCents);

        var invoice = new KsefInvoice
        {
            BusinessId = businessId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalAmountCents = totalCents,
            MissionCount = missions.Count,
            Status = KsefInvoiceStatus.Pending
        };

        db.KsefInvoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        // Fire-and-forget to KSeF service if configured
        _ = TriggerKsefServiceAsync(invoice.Id, ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceStatusDto?> GetInvoiceStatusAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await db.KsefInvoices.FindAsync([invoiceId], ct);
        return invoice is null ? null : ToDto(invoice);
    }

    public async Task<List<InvoiceStatusDto>> GetBusinessInvoicesAsync(int businessId, CancellationToken ct)
    {
        var invoices = await db.KsefInvoices
            .Where(i => i.BusinessId == businessId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return invoices.Select(ToDto).ToList();
    }

    public async Task<bool> UpdateInvoiceStatusAsync(
        Guid invoiceId, KsefInvoiceStatus status, string? referenceNumber, string? error, CancellationToken ct)
    {
        var invoice = await db.KsefInvoices.FindAsync([invoiceId], ct);
        if (invoice is null) return false;

        invoice.Status = status;
        if (referenceNumber is not null) invoice.KsefReferenceNumber = referenceNumber;
        if (error is not null) invoice.ErrorMessage = error;
        if (status == KsefInvoiceStatus.Sent) invoice.SentAt = DateTime.UtcNow;
        if (status == KsefInvoiceStatus.UpoReceived) invoice.UpoReceivedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task TriggerKsefServiceAsync(Guid invoiceId, CancellationToken ct)
    {
        var ksefUrl = config["KSeF:ServiceUrl"] ?? Environment.GetEnvironmentVariable("KSEF_SERVICE_URL");
        if (string.IsNullOrEmpty(ksefUrl)) return;

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(ksefUrl), Timeout = TimeSpan.FromSeconds(30) };
            await http.PostAsJsonAsync("/invoice/process", new { invoiceId }, ct);
        }
        catch
        {
            // KSeF service unavailable — invoice stays in Pending, can be retried manually
        }
    }

    private static InvoiceStatusDto ToDto(KsefInvoice i) => new(
        i.Id, i.BusinessId, i.PeriodStart, i.PeriodEnd,
        i.TotalAmountCents, i.MissionCount, i.Status.ToString(),
        i.KsefReferenceNumber, i.CreatedAt, i.SentAt, i.UpoReceivedAt, i.ErrorMessage);
}
