using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Authorization;

namespace PersonnelPlatform.Infrastructure.Authorization;

public static class AuthorizationDependencyInjection
{
    public static IServiceCollection AddAuthorizationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationRepository, AuthorizationRepository>();
        services.AddScoped<AccessControlService>();
        services.AddScoped<SecurityAdministrationService>();
        return services;
    }
}
