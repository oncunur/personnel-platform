namespace PersonnelPlatform.Application.Identity;

public sealed record AuthPolicyOptions(int MaxFailedLoginAttempts, TimeSpan LockoutDuration);
