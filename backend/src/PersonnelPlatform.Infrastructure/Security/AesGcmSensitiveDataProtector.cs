using System.Security.Cryptography;
using System.Text;
using PersonnelPlatform.Application.Security;

namespace PersonnelPlatform.Infrastructure.Security;

public sealed class AesGcmSensitiveDataProtector : ISensitiveDataProtector
{
    private readonly byte[] key;

    public AesGcmSensitiveDataProtector(string base64Key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Key);
        try { key = Convert.FromBase64String(base64Key); }
        catch (FormatException ex) { throw new InvalidOperationException("Security:DataProtectionKey must be valid base64.", ex); }
        if (key.Length != 32) throw new InvalidOperationException("Security:DataProtectionKey must decode to exactly 32 bytes.");
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var clear = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[clear.Length];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, clear, cipher, tag);
        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        CryptographicOperations.ZeroMemory(clear);
        return "v1:" + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        if (!protectedValue.StartsWith("v1:", StringComparison.Ordinal)) throw new CryptographicException("Unsupported protected data version.");
        var payload = Convert.FromBase64String(protectedValue[3..]);
        if (payload.Length < 28) throw new CryptographicException("Protected data payload is invalid.");
        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var cipher = payload.AsSpan(28);
        var clear = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, clear);
        try { return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
}
