using System.Security.Cryptography;

namespace Synapse.Core.Services.Security;

public interface IEmbeddingEncryptionService
{
    byte[] Encrypt(float[] embedding);
    float[] Decrypt(byte[] ciphertext);
}

public class EmbeddingEncryptionService : IEmbeddingEncryptionService
{
    // Layout: 12B nonce | ciphertext (768 floats * 4B = 3072B) | 16B GCM tag = 3100B total
    private readonly byte[] _key;

    public EmbeddingEncryptionService(IConfiguration config)
    {
        var hex = Environment.GetEnvironmentVariable("EMBEDDING_ENCRYPTION_KEY")
            ?? config["EmbeddingEncryptionKey"];

        if (string.IsNullOrEmpty(hex))
        {
            // Dev fallback — deterministic key, NOT for production
            hex = new string('0', 64);
        }

        _key = Convert.FromHexString(hex);
        if (_key.Length != 32)
            throw new InvalidOperationException("EMBEDDING_ENCRYPTION_KEY must be 64 hex chars (32 bytes)");
    }

    public byte[] Encrypt(float[] embedding)
    {
        var plaintext = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, plaintext, 0, plaintext.Length);

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + ciphertext.Length);
        return result;
    }

    public float[] Decrypt(byte[] data)
    {
        const int nonceLen = 12;
        const int tagLen = 16;

        var nonce = data[..nonceLen];
        var tag = data[^tagLen..];
        var ciphertext = data[nonceLen..^tagLen];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        var floats = new float[plaintext.Length / sizeof(float)];
        Buffer.BlockCopy(plaintext, 0, floats, 0, plaintext.Length);
        return floats;
    }
}
