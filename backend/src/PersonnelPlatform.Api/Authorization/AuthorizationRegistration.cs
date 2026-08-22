using Microsoft.AspNetCore.Authorization;

namespace PersonnelPlatform.Api.Authorization;

public static class AuthorizationRegistration
{
    public static IServiceCollection AddPlatformPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
