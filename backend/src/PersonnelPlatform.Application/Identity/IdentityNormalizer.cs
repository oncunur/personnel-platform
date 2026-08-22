namespace PersonnelPlatform.Application.Identity;

public static class IdentityNormalizer
{
    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();
}
