using System.Security.Cryptography;
using System.Text;
using PersonnelPlatform.Application.Identity;

namespace PersonnelPlatform.Infrastructure.Identity;

public sealed class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public bool TryVerify(string base32Secret, string code, DateTimeOffset now, long? lastAcceptedTimeStep, out long matchedTimeStep)
    {
        matchedTimeStep = -1;
        if (string.IsNullOrWhiteSpace(base32Secret) || code is null || code.Length != Digits || !code.All(char.IsAsciiDigit)) return false;
        byte[] secret;
        try { secret = Base32Decode(base32Secret); }
        catch { return false; }
        var current = now.ToUnixTimeSeconds() / StepSeconds;
        try
        {
            for (var offset = -1; offset <= 1; offset++)
            {
                var step = current + offset;
                if (lastAcceptedTimeStep is not null && step <= lastAcceptedTimeStep.Value) continue;
                var expected = Compute(secret, step);
                if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code)))
                {
                    matchedTimeStep = step;
                    return true;
                }
            }
            return false;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string base32Secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        return $"otpauth://totp/{label}?secret={Uri.EscapeDataString(base32Secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    private static string Compute(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { counter[i] = (byte)(step & 0xff); step >>= 8; }
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Base32Encode(byte[] data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0; var bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b; bitsLeft += 8;
            while (bitsLeft >= 5) { bitsLeft -= 5; output.Append(Alphabet[(buffer >> bitsLeft) & 31]); }
        }
        if (bitsLeft > 0) output.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var cleaned = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0; var bitsLeft = 0;
        foreach (var c in cleaned)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) throw new FormatException("Invalid base32.");
            buffer = (buffer << 5) | index; bitsLeft += 5;
            if (bitsLeft >= 8) { bitsLeft -= 8; output.Add((byte)((buffer >> bitsLeft) & 0xff)); }
        }
        return output.ToArray();
    }
}
