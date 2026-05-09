using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Synapse.KSeF.Models;

namespace Synapse.KSeF.Services;

public interface IKsefAuthService
{
    Task<KsefSession> GetSessionAsync(CancellationToken ct = default);
}

/// <summary>
/// Handles KSeF OAuth challenge-response session management.
/// Test env: https://ksef-test.mf.gov.pl/api
/// Auth flow: POST /online/Session/AuthorisationChallenge → challenge
///            POST /online/Session/InitSessionToken (NIP token, test-only)
///            OR POST /online/Session/InitSessionSignedEncrypted (cert-signed, production)
/// </summary>
public class KsefAuthService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KsefAuthService> logger)
    : IKsefAuthService
{
    private KsefSession? _cachedSession;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<KsefSession> GetSessionAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedSession?.IsValid == true)
                return _cachedSession;

            _cachedSession = await AuthenticateAsync(ct);
            return _cachedSession;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<KsefSession> AuthenticateAsync(CancellationToken ct)
    {
        var http = httpFactory.CreateClient("ksef");
        var nip = config["KSeF:Nip"] ?? Environment.GetEnvironmentVariable("KSEF_NIP")
            ?? throw new InvalidOperationException("KSEF_NIP not configured");

        // Step 1: Get challenge
        var challengeResp = await http.PostAsJsonAsync("/api/online/Session/AuthorisationChallenge",
            new { contextIdentifier = new { type = "onip", identifier = nip } }, ct);
        challengeResp.EnsureSuccessStatusCode();

        var challengeBody = await challengeResp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var challenge = challengeBody.GetProperty("challenge").GetString()!;
        var timestamp = challengeBody.GetProperty("timestamp").GetString()!;

        logger.LogInformation("KSeF challenge received: {Challenge}", challenge);

        // Step 2: Authenticate — use cert if available, fall back to token (test-only)
        var certPath = config["KSeF:CertPath"] ?? Environment.GetEnvironmentVariable("KSEF_CERT_PATH");

        string sessionToken;
        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
        {
            sessionToken = await InitSessionWithCertAsync(http, nip, challenge, timestamp, certPath, ct);
        }
        else
        {
            // Test environment: InitSessionToken (no cert required)
            logger.LogWarning("KSeF cert not found — using token auth (test environment only)");
            sessionToken = await InitSessionTokenAsync(http, nip, challenge, ct);
        }

        return new KsefSession
        {
            SessionToken = sessionToken,
            // Sessions last 3600s; refresh at 3300s to avoid races
            ExpiresAt = DateTime.UtcNow.AddSeconds(3300)
        };
    }

    private static async Task<string> InitSessionTokenAsync(
        HttpClient http, string nip, string challenge, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync("/api/online/Session/InitSessionToken",
            new
            {
                contextIdentifier = new { type = "onip", identifier = nip },
                contextName = new { type = "O", identifier = nip },
                challenge
            }, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("sessionToken").GetProperty("token").GetString()!;
    }

    private static async Task<string> InitSessionWithCertAsync(
        HttpClient http, string nip, string challenge, string timestamp,
        string certPath, CancellationToken ct)
    {
        var certPassword = Environment.GetEnvironmentVariable("KSEF_CERT_PASSWORD") ?? string.Empty;
        var certBytes = await File.ReadAllBytesAsync(certPath, ct);
        using var cert = X509CertificateLoader.LoadPkcs12(certBytes, certPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        // Sign: SHA-256 hash of (challenge|timestamp) using RSA-SHA256
        var message = $"{challenge}|{timestamp}";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        using var rsa = cert.GetRSAPrivateKey()!;
        var signature = rsa.SignData(messageBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureB64 = Convert.ToBase64String(signature);

        var resp = await http.PostAsJsonAsync("/api/online/Session/InitSessionSignedEncrypted",
            new
            {
                contextIdentifier = new { type = "onip", identifier = nip },
                contextName = new { type = "O", identifier = nip },
                challenge,
                signedData = signatureB64
            }, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("sessionToken").GetProperty("token").GetString()!;
    }
}
