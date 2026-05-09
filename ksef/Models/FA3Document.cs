namespace Synapse.KSeF.Models;

/// <summary>Data needed to generate a KSeF FA(3) self-billing invoice.</summary>
public class FA3Document
{
    public Guid InvoiceId { get; set; }

    // Podmiot1 — Synapse (self-billing issuer, P_17=1)
    public string IssuerNip { get; set; } = string.Empty;
    public string IssuerName { get; set; } = string.Empty;
    public string IssuerAddress { get; set; } = string.Empty;

    // Podmiot2 — kawiarnia / business (contractor)
    public string ContractorNip { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string ContractorAddress { get; set; } = string.Empty;

    public DateOnly InvoiceDate { get; set; }
    public DateOnly ServiceDateFrom { get; set; }
    public DateOnly ServiceDateTo { get; set; }

    public int TotalAmountCents { get; set; }
    public int MissionCount { get; set; }

    // Net = Total / 1.23 (VAT 23%)
    public decimal NetAmountPln => Math.Round(TotalAmountCents / 100m / 1.23m, 2);
    public decimal VatAmountPln => Math.Round(TotalAmountCents / 100m - NetAmountPln, 2);
    public decimal GrossAmountPln => TotalAmountCents / 100m;
}
