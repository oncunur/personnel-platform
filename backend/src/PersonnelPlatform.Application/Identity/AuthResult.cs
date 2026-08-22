namespace PersonnelPlatform.Application.Identity;

public sealed record AuthenticatedUser(Guid Id, string Username, string? Email, int SecurityVersion);

public sealed record AuthResult(
    bool Succeeded,
    AuthenticatedUser? User,
    IssuedTokenPair? Tokens,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AuthResult Success(AuthenticatedUser user, IssuedTokenPair tokens) =>
        new(true, user, tokens, null, null);

    public static AuthResult Failure(string code, string message) =>
        new(false, null, null, code, message);
}
