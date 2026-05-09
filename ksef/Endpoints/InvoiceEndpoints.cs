using System.Net.Http.Json;
using System.Text.Json;
using Synapse.KSeF.Models;
using Synapse.KSeF.Services;

namespace Synapse.KSeF.Endpoints;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        // POST /invoice/process — called by main backend to initiate FA(3) generation
        app.MapPost("/invoice/process", async (
            InvoiceProcessRequest req,
            IKsefAuthService authSvc,
            IInvoiceGeneratorService generatorSvc,
            IEncryptionService encryptionSvc,
            UpoPollerService pollerSvc,
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation("Processing invoice {InvoiceId}", req.InvoiceId);

            // Fetch invoice details from main backend
            var backendUrl = config["KSeF:BackendUrl"] ?? Environment.GetEnvironmentVariable("KSEF_BACKEND_URL");
            if (string.IsNullOrEmpty(backendUrl))
                return Results.Problem("KSEF_BACKEND_URL not configured.");

            FA3Document doc;
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(backendUrl), Timeout = TimeSpan.FromSeconds(15) };
                var secret = Environment.GetEnvironmentVariable("INTERNAL_API_SECRET") ?? config["InternalApiSecret"];
                if (!string.IsNullOrEmpty(secret))
                    http.DefaultRequestHeaders.Add("X-Internal-Secret", secret);

                var invoiceResp = await http.GetAsync($"/api/invoices/{req.InvoiceId}", ct);
                if (!invoiceResp.IsSuccessStatusCode)
                    return Results.NotFound(new { error = "Invoice not found in backend" });

                var invoiceData = await invoiceResp.Content.ReadFromJsonAsync<JsonElement>(ct);
                doc = BuildDocument(req.InvoiceId, invoiceData, config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch invoice {InvoiceId} from backend", req.InvoiceId);
                return Results.Problem("Failed to fetch invoice data.");
            }

            // Generate XML
            var xml = generatorSvc.GenerateFA3Xml(doc);

            // Encrypt for KSeF
            var encrypted = encryptionSvc.EncryptForKSeF(xml);

            // Get session and send to KSeF
            try
            {
                var session = await authSvc.GetSessionAsync(ct);
                var http = httpFactory.CreateClient("ksef");
                http.DefaultRequestHeaders.Add("SessionToken", session.SessionToken);

                var sendResp = await http.PostAsJsonAsync("/api/online/Invoice/Send",
                    new
                    {
                        invoiceHash = new
                        {
                            hashSHA = new
                            {
                                algorithm = "SHA-256",
                                encoding = "Base64",
                                value = ComputeSha256Base64(xml)
                            },
                            fileSize = System.Text.Encoding.UTF8.GetByteCount(xml)
                        },
                        invoicePayload = new
                        {
                            type = "encrypted",
                            initUpload = encrypted
                        }
                    }, ct);

                sendResp.EnsureSuccessStatusCode();
                var sendResult = await sendResp.Content.ReadFromJsonAsync<JsonElement>(ct);
                var refNumber = sendResult.GetProperty("elementReferenceNumber").GetString()!;

                logger.LogInformation("Invoice {InvoiceId} sent to KSeF, ref: {Ref}", req.InvoiceId, refNumber);

                // Notify backend: status = Sent
                using var notifyHttp = new HttpClient { BaseAddress = new Uri(backendUrl) };
                var secret = Environment.GetEnvironmentVariable("INTERNAL_API_SECRET") ?? config["InternalApiSecret"];
                if (!string.IsNullOrEmpty(secret))
                    notifyHttp.DefaultRequestHeaders.Add("X-Internal-Secret", secret);
                await notifyHttp.PostAsJsonAsync($"/api/invoices/{req.InvoiceId}/status",
                    new { Status = "Sent", ReferenceNumber = refNumber, ErrorMessage = (string?)null }, ct);

                // Start UPO polling
                pollerSvc.EnqueueJob(req.InvoiceId, refNumber);

                return Results.Ok(new { referenceNumber = refNumber });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send invoice {InvoiceId} to KSeF", req.InvoiceId);
                return Results.Problem($"KSeF send failed: {ex.Message}");
            }
        });

        // GET /invoice/{id}/status — proxy to backend status
        app.MapGet("/invoice/{id:guid}/status", async (
            Guid id,
            IHttpClientFactory httpFactory,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var backendUrl = config["KSeF:BackendUrl"] ?? Environment.GetEnvironmentVariable("KSEF_BACKEND_URL");
            if (string.IsNullOrEmpty(backendUrl)) return Results.Problem("Backend URL not configured.");

            using var http = new HttpClient { BaseAddress = new Uri(backendUrl) };
            var resp = await http.GetAsync($"/api/invoices/{id}", ct);
            var content = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode ? Results.Content(content, "application/json") : Results.NotFound();
        });
    }

    private static FA3Document BuildDocument(Guid invoiceId, JsonElement data, IConfiguration config)
    {
        var periodStart = DateOnly.Parse(data.GetProperty("periodStart").GetString()!);
        var periodEnd = DateOnly.Parse(data.GetProperty("periodEnd").GetString()!);

        return new FA3Document
        {
            InvoiceId = invoiceId,
            IssuerNip = config["KSeF:Nip"] ?? Environment.GetEnvironmentVariable("KSEF_NIP") ?? "0000000000",
            IssuerName = config["KSeF:CompanyName"] ?? "Synapse Sp. z o.o.",
            IssuerAddress = config["KSeF:CompanyAddress"] ?? "ul. Synapse 1, 30-001 Kraków",
            ContractorNip = data.TryGetProperty("contractorNip", out var nip) ? nip.GetString() ?? "0000000000" : "0000000000",
            ContractorName = data.TryGetProperty("contractorName", out var name) ? name.GetString() ?? "Partner" : "Partner",
            ContractorAddress = data.TryGetProperty("contractorAddress", out var addr) ? addr.GetString() ?? string.Empty : string.Empty,
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ServiceDateFrom = periodStart,
            ServiceDateTo = periodEnd,
            TotalAmountCents = data.GetProperty("totalAmountCents").GetInt32(),
            MissionCount = data.GetProperty("missionCount").GetInt32()
        };
    }

    private static string ComputeSha256Base64(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

public record InvoiceProcessRequest(Guid InvoiceId);
