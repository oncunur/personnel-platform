namespace PersonnelPlatform.Application.Identity;

public sealed record AuthPolicyOptions(
    int MaxFailedLoginAttempts,
    TimeSpan LockoutDuration,
    bool MfaEnabled,
    TimeSpan MfaChallengeLifetime,
    string MfaIssuer,
    IReadOnlySet<string> MfaRequiredRoles)
{
    public bool RequiresMfa(IEnumerable<string> roleCodes) => MfaEnabled && roleCodes.Any(x => MfaRequiredRoles.Contains(x));
}
