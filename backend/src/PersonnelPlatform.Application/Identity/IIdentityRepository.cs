using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public interface IIdentityRepository
{
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshToken>> ListActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddRefreshToken(RefreshToken refreshToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
