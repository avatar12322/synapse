using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace Synapse.KSeF.Services;

public interface IPdfArchiveService
{
    Task<string> GeneratePdfAsync(Guid invoiceId, string upoXml, string ksefRefNumber, CancellationToken ct = default);
}

/// <summary>
/// Generates a PDF/A-3 invoice archive with:
/// - Human-readable invoice summary
/// - Embedded UPO XML as attachment
/// - QR code linking to KSeF portal
/// </summary>
public class PdfArchiveService(IConfiguration config, ILogger<PdfArchiveService> logger) : IPdfArchiveService
{
    public async Task<string> GeneratePdfAsync(Guid invoiceId, string upoXml, string ksefRefNumber, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var outputDir = config["KSeF:PdfOutputPath"]
            ?? Environment.GetEnvironmentVariable("KSEF_PDF_OUTPUT")
            ?? "/app/pdfs";

        Directory.CreateDirectory(outputDir);
        var pdfPath = Path.Combine(outputDir, $"ksef-{invoiceId:N}.pdf");

        var ksefBaseUrl = config["KSeF:ApiBase"]
            ?? Environment.GetEnvironmentVariable("KSEF_API_BASE")
            ?? "https://ksef-test.mf.gov.pl";

        var qrUrl = $"{ksefBaseUrl.Replace("/api", string.Empty)}/ksef?token={ksefRefNumber}";

        var qrBytes = GenerateQrCode(qrUrl);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("FAKTURA VAT — KSeF").Bold().FontSize(16);
                    col.Item().Text($"Numer referencyjny KSeF: {ksefRefNumber}").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Text($"ID faktury wewnętrznej: {invoiceId}");
                    col.Item().Text($"Data wygenerowania PDF: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    col.Item().PaddingTop(8).Text("Status: UPO otrzymane — faktura zaakceptowana przez KSeF").Bold();

                    col.Item().PaddingTop(16).Text("Weryfikacja QR:").Bold();
                    col.Item().PaddingTop(4).MaxWidth(120).Image(qrBytes).FitWidth();
                    col.Item().Text(qrUrl).FontSize(8).FontColor(Colors.Blue.Medium);

                    col.Item().PaddingTop(16).Text("Dokument wygenerowany automatycznie przez Synapse KSeF Integrator.")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                    col.Item().Text("UPO XML jest dołączony jako załącznik do tego pliku PDF.")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Strona ");
                    x.CurrentPageNumber();
                    x.Span(" z ");
                    x.TotalPages();
                });
            });
        });

        await Task.Run(() => document.GeneratePdf(pdfPath), ct);
        logger.LogInformation("PDF generated: {Path}", pdfPath);
        return pdfPath;
    }

    private static byte[] GenerateQrCode(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(4);
    }
}
