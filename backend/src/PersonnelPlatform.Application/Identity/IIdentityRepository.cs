using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public interface IIdentityRepository
{
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddRefreshToken(RefreshToken refreshToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
