using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace Synapse.KSeF.Services;

/// <summary>
/// Background service that polls KSeF for UPO (Urzędowe Potwierdzenie Odbioru)
/// after an invoice is sent. Uses exponential backoff: 5s → 10s → 30s → 60s (max 20 tries).
/// On success: downloads UPO XML, generates PDF/A-3, notifies main backend.
/// </summary>
public class UpoPollerService(
    IKsefAuthService authSvc,
    IPdfArchiveService pdfSvc,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<UpoPollerService> logger) : BackgroundService
{
    private readonly Channel<UpoJob> _queue = System.Threading.Channels.Channel.CreateUnbounded<UpoJob>();

    public void EnqueueJob(Guid invoiceId, string ksefReferenceNumber)
        => _queue.Writer.TryWrite(new UpoJob(invoiceId, ksefReferenceNumber));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct))
        {
            _ = ProcessJobAsync(job, ct);
        }
    }

    private async Task ProcessJobAsync(UpoJob job, CancellationToken ct)
    {
        var delays = new[] { 5, 10, 30, 60 };
        var attempt = 0;
        const int maxAttempts = 20;

        while (attempt < maxAttempts && !ct.IsCancellationRequested)
        {
            try
            {
                var session = await authSvc.GetSessionAsync(ct);
                var http = httpFactory.CreateClient("ksef");
                http.DefaultRequestHeaders.Add("SessionToken", session.SessionToken);

                var statusResp = await http.GetAsync(
                    $"/api/online/Invoice/Status/{job.KsefReferenceNumber}", ct);

                if (!statusResp.IsSuccessStatusCode)
                {
                    logger.LogWarning("UPO status check failed for {InvoiceId}, attempt {Attempt}",
                        job.InvoiceId, attempt + 1);
                    await WaitAsync(delays, attempt, ct);
                    attempt++;
                    continue;
                }

                var status = await statusResp.Content.ReadFromJsonAsync<JsonElement>(ct);
                var code = status.GetProperty("processingCode").GetInt32();

                if (code == 200)
                {
                    // Still processing
                    await WaitAsync(delays, attempt, ct);
                    attempt++;
                    continue;
                }

                if (code == 300)
                {
                    // Success — fetch UPO
                    var elemRef = status.GetProperty("elementReferenceNumber").GetString()!;
                    var upoResp = await http.GetAsync($"/api/online/Invoice/UPO/{elemRef}", ct);
                    var upoXml = await upoResp.Content.ReadAsStringAsync(ct);

                    var pdfPath = await pdfSvc.GeneratePdfAsync(job.InvoiceId, upoXml, job.KsefReferenceNumber, ct);

                    await NotifyBackendAsync(job.InvoiceId, "UpoReceived", job.KsefReferenceNumber, null, pdfPath, ct);
                    logger.LogInformation("UPO received for invoice {InvoiceId}", job.InvoiceId);
                    return;
                }

                // code 400 = error
                logger.LogError("KSeF rejected invoice {InvoiceId}: processingCode={Code}", job.InvoiceId, code);
                await NotifyBackendAsync(job.InvoiceId, "Failed", null, $"KSeF processingCode={code}", null, ct);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UPO poll error for {InvoiceId}, attempt {Attempt}", job.InvoiceId, attempt + 1);
                await WaitAsync(delays, attempt, ct);
                attempt++;
            }
        }

        if (attempt >= maxAttempts)
        {
            logger.LogError("UPO poll exhausted for {InvoiceId}", job.InvoiceId);
            await NotifyBackendAsync(job.InvoiceId, "Failed", null, "UPO poll max attempts exceeded", null, ct);
        }
    }

    private async Task NotifyBackendAsync(Guid invoiceId, string status, string? refNumber,
        string? error, string? pdfPath, CancellationToken ct)
    {
        var backendUrl = config["KSeF:BackendUrl"] ?? Environment.GetEnvironmentVariable("KSEF_BACKEND_URL");
        if (string.IsNullOrEmpty(backendUrl)) return;

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(backendUrl), Timeout = TimeSpan.FromSeconds(15) };
            var secret = Environment.GetEnvironmentVariable("INTERNAL_API_SECRET") ?? config["InternalApiSecret"];
            if (!string.IsNullOrEmpty(secret))
                http.DefaultRequestHeaders.Add("X-Internal-Secret", secret);

            await http.PostAsJsonAsync($"/api/invoices/{invoiceId}/status",
                new { Status = status, ReferenceNumber = refNumber, ErrorMessage = error }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify backend about invoice {InvoiceId}", invoiceId);
        }
    }

    private static Task WaitAsync(int[] delays, int attempt, CancellationToken ct)
    {
        var delay = delays[Math.Min(attempt, delays.Length - 1)];
        return Task.Delay(TimeSpan.FromSeconds(delay), ct);
    }

    private record UpoJob(Guid InvoiceId, string KsefReferenceNumber);
}
