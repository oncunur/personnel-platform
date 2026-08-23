namespace PersonnelPlatform.Application.Security;

public interface ISensitiveDataProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public static class SensitiveDataMasking
{
    public static string? MaskNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 4 ? new string('*', normalized.Length) : new string('*', normalized.Length - 4) + normalized[^4..];
    }

    public static string? MaskIban(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (normalized.Length <= 8) return new string('*', normalized.Length);
        return normalized[..2] + new string('*', normalized.Length - 6) + normalized[^4..];
    }

    public static string MaskMoney(decimal value, string currency) => $"*** {currency.Trim().ToUpperInvariant()}";
}
