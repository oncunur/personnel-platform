using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Infrastructure.Security;

namespace PersonnelPlatform.Infrastructure.Identity;

public static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var issuer = GetRequired(configuration, "Jwt:Issuer");
        var audience = GetRequired(configuration, "Jwt:Audience");
        var signingKey = GetRequired(configuration, "Jwt:SigningKey");
        var dataProtectionKey = GetRequired(configuration, "Security:DataProtectionKey");
        var accessTokenMinutes = GetPositiveInt(configuration, "Jwt:AccessTokenMinutes", 15);
        var refreshTokenDays = GetPositiveInt(configuration, "Jwt:RefreshTokenDays", 7);
        var maxFailedAttempts = GetPositiveInt(configuration, "Identity:MaxFailedLoginAttempts", 5);
        var lockoutMinutes = GetPositiveInt(configuration, "Identity:LockoutMinutes", 15);
        var mfaChallengeMinutes = GetPositiveInt(configuration, "Identity:MfaChallengeMinutes", 5);
        var mfaIssuer = configuration["Identity:MfaIssuer"]?.Trim();
        if (string.IsNullOrWhiteSpace(mfaIssuer)) mfaIssuer = issuer;
        var requiredRoles = (configuration["Identity:MfaRequiredRoles"] ?? "SYSTEM_ADMIN,SECURITY_ADMIN,PAYROLL_SPECIALIST,HR_MANAGER")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        services.AddSingleton(new JwtTokenOptions(issuer, audience, signingKey, TimeSpan.FromMinutes(accessTokenMinutes), TimeSpan.FromDays(refreshTokenDays)));
        services.AddSingleton(new AuthPolicyOptions(maxFailedAttempts, TimeSpan.FromMinutes(lockoutMinutes), TimeSpan.FromMinutes(mfaChallengeMinutes), mfaIssuer, requiredRoles));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuthTokenService, JwtTokenService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISensitiveDataProtector>(new AesGcmSensitiveDataProtector(dataProtectionKey));
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        return services;
    }

    private static string GetRequired(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration value '{key}' is required.");

    private static int GetPositiveInt(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw, out var value) && value > 0 ? value : throw new InvalidOperationException($"Configuration value '{key}' must be a positive integer.");
    }
}
