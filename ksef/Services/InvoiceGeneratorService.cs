using System.Xml.Linq;
using Synapse.KSeF.Models;

namespace Synapse.KSeF.Services;

public interface IInvoiceGeneratorService
{
    string GenerateFA3Xml(FA3Document doc);
}

/// <summary>
/// Generates KSeF FA(3) XML invoice for self-billing (samofakturowanie, P_17=1).
/// Schema: http://crd.gov.pl/wzor/2023/06/29/12648/
/// </summary>
public class InvoiceGeneratorService : IInvoiceGeneratorService
{
    private static readonly XNamespace Fa = "http://crd.gov.pl/wzor/2023/06/29/12648/";
    private static readonly XNamespace Etd = "http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypow/";

    public string GenerateFA3Xml(FA3Document doc)
    {
        var invoiceNumber = $"SYN/{doc.InvoiceDate:yyyy/MM}/{doc.InvoiceId.ToString()[..8].ToUpper()}";

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Fa + "Faktura",
                new XAttribute("xmlns", Fa.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "etd", Etd.NamespaceName),

                // Nagłówek
                new XElement(Fa + "Naglowek",
                    new XElement(Fa + "KodFormularza",
                        new XAttribute("kodSystemowy", "FA (3)"),
                        new XAttribute("wersjaSchemy", "1-0E"),
                        "FA"),
                    new XElement(Fa + "WariantFormularza", "3"),
                    new XElement(Fa + "DataWytworzeniaFa", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(Fa + "SystemInfo", "Synapse/1.0")
                ),

                // Podmiot1 — Synapse (issuer, self-billing)
                new XElement(Fa + "Podmiot1",
                    new XElement(Fa + "DaneIdentyfikacyjne",
                        new XElement(Fa + "NIP", doc.IssuerNip),
                        new XElement(Fa + "PelnaNazwa", doc.IssuerName)
                    ),
                    new XElement(Fa + "Adres",
                        new XElement(Fa + "AdresL1", doc.IssuerAddress)
                    ),
                    // P_17=1 — self-billing clause (samofakturowanie)
                    new XElement(Fa + "P_17", "1")
                ),

                // Podmiot2 — kawiarnia (contractor)
                new XElement(Fa + "Podmiot2",
                    new XElement(Fa + "DaneIdentyfikacyjne",
                        new XElement(Fa + "NIP", doc.ContractorNip),
                        new XElement(Fa + "PelnaNazwa", doc.ContractorName)
                    ),
                    new XElement(Fa + "Adres",
                        new XElement(Fa + "AdresL1", doc.ContractorAddress)
                    )
                ),

                // Fa — invoice details
                new XElement(Fa + "Fa",
                    new XElement(Fa + "KodWaluty", "PLN"),
                    new XElement(Fa + "P_1", doc.InvoiceDate.ToString("yyyy-MM-dd")),
                    new XElement(Fa + "P_1M", invoiceNumber),
                    new XElement(Fa + "P_6", doc.ServiceDateFrom.ToString("yyyy-MM-dd")),
                    new XElement(Fa + "RodzajFaktury", "VAT"),

                    // Line item — aggregated commission for period
                    new XElement(Fa + "FaWiersz",
                        new XElement(Fa + "NrWierszaFa", "1"),
                        new XElement(Fa + "P_7", $"Prowizja Synapse — misje {doc.ServiceDateFrom:yyyy-MM-dd}:{doc.ServiceDateTo:yyyy-MM-dd} ({doc.MissionCount} misji)"),
                        new XElement(Fa + "P_8A", "szt."),
                        new XElement(Fa + "P_8B", doc.MissionCount.ToString()),
                        new XElement(Fa + "P_9A", (doc.NetAmountPln / doc.MissionCount).ToString("F2")),
                        new XElement(Fa + "P_11", doc.NetAmountPln.ToString("F2")),
                        new XElement(Fa + "P_12", "23")
                    ),

                    // VAT summary
                    new XElement(Fa + "P_13_1", doc.NetAmountPln.ToString("F2")),
                    new XElement(Fa + "P_14_1", doc.VatAmountPln.ToString("F2")),
                    new XElement(Fa + "P_15", doc.GrossAmountPln.ToString("F2")),

                    // Payment
                    new XElement(Fa + "Adnotacje",
                        new XElement(Fa + "P_16", "2"),  // 2 = not a reverse-charge
                        new XElement(Fa + "P_17", "1"),  // self-billing
                        new XElement(Fa + "P_18", "2"),
                        new XElement(Fa + "P_18A", "2"),
                        new XElement(Fa + "P_19", "2"),
                        new XElement(Fa + "P_19A", "2"),
                        new XElement(Fa + "P_19B", "2"),
                        new XElement(Fa + "P_19C", "2"),
                        new XElement(Fa + "P_20", "2"),
                        new XElement(Fa + "P_21", "2"),
                        new XElement(Fa + "P_22", "2"),
                        new XElement(Fa + "P_23", "2"),
                        new XElement(Fa + "P_PMarzy", "2")
                    ),

                    new XElement(Fa + "RodzajFakturyDla", "B2B")
                )
            )
        );

        return xml.Declaration + Environment.NewLine + xml.ToString(SaveOptions.None);
    }
}
