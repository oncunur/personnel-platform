using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PersonnelPlatform.Api.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string PolicyPrefix = "Permission:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal)) return base.GetPolicyAsync(policyName);
        var permissionCode = policyName[PolicyPrefix.Length..];
        if (string.IsNullOrWhiteSpace(permissionCode)) return Task.FromResult<AuthorizationPolicy?>(null);
        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
