using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Identity;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Identity;

public sealed class IdentityRepository(ApplicationDbContext dbContext) : IIdentityRepository
{
    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, cancellationToken);

    public Task<User?> FindUserByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername && x.DeletedAt == null, cancellationToken);

    public Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.Where(x => x.DeletedAt == null).OrderBy(x => x.Username).ToListAsync(cancellationToken);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveRefreshTokensAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public void AddUser(User user) => dbContext.Users.Add(user);
    public void AddRefreshToken(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
