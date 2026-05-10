using Synapse.Core.Models.Invoice;

namespace Synapse.Core.Services.Invoice;

public interface IJurisdictionService
{
    decimal GetDefaultVatRate(string countryCode);
    InvoiceType GetInvoiceType(string countryCode);
}

public class JurisdictionService : IJurisdictionService
{
    private static readonly Dictionary<string, decimal> VatRates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PL"] = 0.23m,
            ["DE"] = 0.19m,
            ["FR"] = 0.20m,
            ["CZ"] = 0.21m,
            ["SK"] = 0.20m,
            ["AT"] = 0.20m,
            ["NL"] = 0.21m,
            ["BE"] = 0.21m,
            ["ES"] = 0.21m,
            ["IT"] = 0.22m,
        };

    public decimal GetDefaultVatRate(string countryCode)
        => VatRates.TryGetValue(countryCode, out var rate) ? rate : 0.23m;

    public InvoiceType GetInvoiceType(string countryCode)
        => countryCode.Equals("PL", StringComparison.OrdinalIgnoreCase)
            ? InvoiceType.KSeF
            : InvoiceType.EuPdf;
}
