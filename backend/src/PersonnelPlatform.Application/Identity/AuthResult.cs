namespace PersonnelPlatform.Application.Identity;

public sealed record AuthenticatedUser(Guid Id, string Username, string? Email, int SecurityVersion);

public sealed record MfaChallengeInfo(
    string ChallengeToken,
    string Purpose,
    bool EnrollmentRequired,
    string? EnrollmentSecret,
    string? OtpAuthUri,
    DateTimeOffset ExpiresAt);

public sealed record AuthResult(
    bool Succeeded,
    AuthenticatedUser? User,
    IssuedTokenPair? Tokens,
    MfaChallengeInfo? Mfa,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AuthResult Success(AuthenticatedUser user, IssuedTokenPair tokens) => new(true, user, tokens, null, null, null);
    public static AuthResult MfaRequired(AuthenticatedUser user, MfaChallengeInfo challenge) => new(true, user, null, challenge, null, null);
    public static AuthResult Failure(string code, string message) => new(false, null, null, null, code, message);
}
