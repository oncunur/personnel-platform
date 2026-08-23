using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PersonnelPlatform.Application.Identity;

namespace PersonnelPlatform.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddPlatformAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is required.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is required.");
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var securityVersionRaw = context.Principal?.FindFirst("sv")?.Value;

                        if (!Guid.TryParse(subject, out var userId)
                            || !int.TryParse(securityVersionRaw, out var tokenSecurityVersion))
                        {
                            context.Fail("Token security claims are invalid.");
                            return;
                        }

                        var repository = context.HttpContext.RequestServices.GetRequiredService<IIdentityRepository>();
                        var user = await repository.FindUserByIdAsync(userId, context.HttpContext.RequestAborted);

                        if (user is null || !user.IsActive || user.SecurityVersion != tokenSecurityVersion)
                        {
                            context.Fail("Token session has been invalidated.");
                        }
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
