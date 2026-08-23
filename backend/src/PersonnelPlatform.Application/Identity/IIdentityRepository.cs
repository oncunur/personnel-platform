using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public interface IIdentityRepository
{
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken);
    Task<User?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListActiveRoleCodesAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefreshToken>> ListActiveRefreshTokensAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<UserMfaCredential?> FindMfaCredentialAsync(Guid userId, CancellationToken cancellationToken);
    Task<MfaChallenge?> FindMfaChallengeByHashAsync(string tokenHash, CancellationToken cancellationToken);
    void AddUser(User user);
    void AddRefreshToken(RefreshToken refreshToken);
    void AddMfaCredential(UserMfaCredential credential);
    void AddMfaChallenge(MfaChallenge challenge);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
