using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedDevelopmentIdentityAsync(this IServiceProvider serviceProvider, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var username = configuration["BootstrapAdmin:Username"];
        var password = configuration["BootstrapAdmin:Password"];
        var email = configuration["BootstrapAdmin:Email"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;
        if (password.Length < 12) throw new InvalidOperationException("BootstrapAdmin:Password must contain at least 12 characters.");

        await using var scope = serviceProvider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedUsername = IdentityNormalizer.NormalizeUsername(username);
        var now = TimeProvider.System.GetUtcNow();

        var user = await repository.FindUserByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null)
        {
            user = User.Create(username, normalizedUsername, email, IdentityNormalizer.NormalizeEmail(email), passwordHasher.Hash(password), now);
            repository.AddUser(user);
            await repository.SaveChangesAsync(cancellationToken);
        }

        var systemAdminRole = await dbContext.Roles.SingleOrDefaultAsync(x => x.Code == "SYSTEM_ADMIN" && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("SYSTEM_ADMIN role seed is missing.");

        if (!await dbContext.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == systemAdminRole.Id, cancellationToken))
            dbContext.UserRoles.Add(UserRole.Create(user.Id, systemAdminRole.Id, now, user.Id));

        if (!await dbContext.UserScopes.AnyAsync(x => x.UserId == user.Id && x.ScopeType == ScopeTypes.Global && x.ScopeId == null && x.IsActive, cancellationToken))
            dbContext.UserScopes.Add(UserScope.Create(user.Id, ScopeTypes.Global, null, now, null, now, user.Id));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
