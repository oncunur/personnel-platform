using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedDevelopmentIdentityAsync(
        this IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var username = configuration["BootstrapAdmin:Username"];
        var password = configuration["BootstrapAdmin:Password"];
        var email = configuration["BootstrapAdmin:Email"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (password.Length < 12)
        {
            throw new InvalidOperationException("BootstrapAdmin:Password must contain at least 12 characters.");
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdentityRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedUsername = IdentityNormalizer.NormalizeUsername(username);

        var existing = await repository.FindUserByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var now = TimeProvider.System.GetUtcNow();
        var user = User.Create(
            username,
            normalizedUsername,
            email,
            IdentityNormalizer.NormalizeEmail(email),
            passwordHasher.Hash(password),
            now);

        repository.AddUser(user);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
