using System.ComponentModel.DataAnnotations;

namespace Synapse.Core.Models.Invoice;

public class KsefInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int BusinessId { get; set; }
    public Business.Business Business { get; set; } = null!;

    // Phase 4: discriminator — KSeF for PL businesses, EuPdf for other EU
    public InvoiceType InvoiceType { get; set; } = InvoiceType.KSeF;

    // Phase 4: VAT rate snapshot at invoice creation time (from tenant config or JurisdictionService)
    public decimal VatRatePct { get; set; } = 0.23m;

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public int TotalAmountCents { get; set; }
    public int MissionCount { get; set; }

    [MaxLength(100)]
    public string? KsefReferenceNumber { get; set; }

    public KsefInvoiceStatus Status { get; set; } = KsefInvoiceStatus.Pending;

    public string? UpoXml { get; set; }

    [MaxLength(500)]
    public string? PdfPath { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? UpoReceivedAt { get; set; }
}

public enum KsefInvoiceStatus
{
    Pending = 0,
    Sent = 1,
    UpoReceived = 2,
    Failed = 3
}

public enum InvoiceType
{
    KSeF = 0,
    EuPdf = 1
}
