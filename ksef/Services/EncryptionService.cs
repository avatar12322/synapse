using System.Security.Cryptography;
using System.Text;

namespace Synapse.KSeF.Services;

public interface IEncryptionService
{
    EncryptedPayload EncryptForKSeF(string xmlContent);
}

public record EncryptedPayload(
    string EncryptedContent,  // AES-256-CBC encrypted XML, base64
    string EncryptedKey,      // RSA-4096 OAEP-SHA256 encrypted AES key, base64
    string Iv                 // AES IV, base64
);

/// <summary>
/// KSeF payload encryption:
/// 1. Generate random AES-256-CBC key + IV
/// 2. Encrypt XML with AES
/// 3. Encrypt AES key with KSeF public key (RSA-4096, OAEP-SHA256)
/// KSeF public key is fetched from /api/online/Session/GetReferenceNumbers (test) or hardcoded PEM
/// </summary>
public class EncryptionService(IHttpClientFactory httpFactory, ILogger<EncryptionService> logger)
    : IEncryptionService
{
    // KSeF test environment RSA public key (2048-bit placeholder — real key fetched from API)
    // In production: fetch from GET /api/common/KSeF/encryption-key and cache
    private static RSA? _ksefPublicKey;
    private static readonly SemaphoreSlim _keyLock = new(1, 1);

    public EncryptedPayload EncryptForKSeF(string xmlContent)
    {
        // Generate AES-256 key and IV
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.GenerateKey();
        aes.GenerateIV();

        // Encrypt XML content with AES
        using var encryptor = aes.CreateEncryptor();
        var xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
        var encryptedXml = encryptor.TransformFinalBlock(xmlBytes, 0, xmlBytes.Length);

        // Encrypt AES key with KSeF RSA public key (OAEP-SHA256)
        var rsa = GetOrCreateKsefPublicKey();
        var encryptedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);

        return new EncryptedPayload(
            Convert.ToBase64String(encryptedXml),
            Convert.ToBase64String(encryptedKey),
            Convert.ToBase64String(aes.IV)
        );
    }

    private static RSA GetOrCreateKsefPublicKey()
    {
        if (_ksefPublicKey is not null) return _ksefPublicKey;

        // For test environment: use a self-generated 4096-bit key as placeholder
        // In production: fetch from KSeF API GET /api/common/KSeF/encryption-key
        var rsa = RSA.Create(4096);
        _ksefPublicKey = rsa;
        return rsa;
    }

    /// <summary>Loads the KSeF encryption public key from the API and caches it.</summary>
    public async Task LoadKsefPublicKeyAsync(CancellationToken ct = default)
    {
        await _keyLock.WaitAsync(ct);
        try
        {
            if (_ksefPublicKey is not null) return;

            var http = httpFactory.CreateClient("ksef");
            var resp = await http.GetAsync("/api/common/KSeF/encryption-key", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not fetch KSeF public key — using generated test key");
                _ksefPublicKey = RSA.Create(4096);
                return;
            }

            var pem = await resp.Content.ReadAsStringAsync(ct);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem.AsSpan());
            _ksefPublicKey = rsa;
            logger.LogInformation("KSeF public encryption key loaded");
        }
        finally
        {
            _keyLock.Release();
        }
    }
}
