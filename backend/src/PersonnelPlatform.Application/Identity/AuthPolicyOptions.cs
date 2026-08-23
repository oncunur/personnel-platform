namespace PersonnelPlatform.Application.Identity;

public sealed record AuthPolicyOptions(
    int MaxFailedLoginAttempts,
    TimeSpan LockoutDuration,
    TimeSpan MfaChallengeLifetime,
    string MfaIssuer,
    IReadOnlySet<string> MfaRequiredRoles)
{
    public bool RequiresMfa(IEnumerable<string> roleCodes) => roleCodes.Any(x => MfaRequiredRoles.Contains(x));
}
