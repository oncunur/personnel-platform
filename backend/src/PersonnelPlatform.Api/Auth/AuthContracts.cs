using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Api.Auth;

public sealed record LoginRequest(string Username, string Password);
public sealed record MfaCompleteRequest(string ChallengeToken, string Code);
public sealed record AuthResponse(Guid UserId, string Username, string? Email, string AccessToken, DateTimeOffset AccessTokenExpiresAt);
public sealed record MfaRequiredResponse(Guid UserId, string Username, string ChallengeToken, string Purpose, bool EnrollmentRequired, string? EnrollmentSecret, string? OtpAuthUri, DateTimeOffset ExpiresAt);
public sealed record MeResponse(Guid UserId, string Username, string? Email, int SecurityVersion, bool MfaVerified, IReadOnlyList<RoleSummary> Roles, IReadOnlyList<PermissionSummary> Permissions, IReadOnlyList<ScopeSummary> Scopes);
