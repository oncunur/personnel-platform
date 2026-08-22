using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public sealed class AuthService(
    IIdentityRepository identityRepository,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokenService,
    AuthPolicyOptions policyOptions,
    TimeProvider timeProvider)
{
    // A valid PBKDF2 hash is used for unknown users to reduce username-enumeration timing differences.
    private const string DummyPasswordHash = "pbkdf2-sha512$210000$ZHVtbXktc2FsdC0xNmJ5dGU=$Umnbcv8kiYHPAqX5lY5kK5X6bZoTgij1b9wsLH/r7WQ=";

    public async Task<AuthResult> LoginAsync(
        string username,
        string password,
        string? ipAddress,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");
        }

        var normalizedUsername = IdentityNormalizer.NormalizeUsername(username);
        var user = await identityRepository.FindUserByNormalizedUsernameAsync(normalizedUsername, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _ = passwordHasher.Verify(password, DummyPasswordHash);
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");
        }

        var now = timeProvider.GetUtcNow();

        if (user.IsLockedAt(now))
        {
            return AuthResult.Failure("AUTH_ACCOUNT_LOCKED", "Hesap geçici olarak kilitli.");
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            var locked = user.RegisterFailedLogin(
                now,
                policyOptions.MaxFailedLoginAttempts,
                policyOptions.LockoutDuration);

            await identityRepository.SaveChangesAsync(cancellationToken);

            return locked
                ? AuthResult.Failure("AUTH_ACCOUNT_LOCKED", "Hesap geçici olarak kilitli.")
                : AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");
        }

        user.RegisterSuccessfulLogin(now);
        var issued = tokenService.Issue(user, now);

        identityRepository.AddRefreshToken(RefreshToken.Create(
            user.Id,
            issued.RefreshTokenHash,
            user.SecurityVersion,
            issued.RefreshTokenExpiresAt,
            now,
            ipAddress,
            NormalizeDeviceName(deviceName)));

        await identityRepository.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(ToAuthenticatedUser(user), issued);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 2048)
        {
            return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var storedToken = await identityRepository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActiveAt(now))
        {
            return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");
        }

        var user = await identityRepository.FindUserByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null
            || !user.IsActive
            || user.IsLockedAt(now)
            || storedToken.SecurityVersion != user.SecurityVersion)
        {
            storedToken.Revoke(now, ipAddress);
            await identityRepository.SaveChangesAsync(cancellationToken);
            return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");
        }

        var issued = tokenService.Issue(user, now);
        storedToken.Rotate(now, issued.RefreshTokenHash, ipAddress);

        identityRepository.AddRefreshToken(RefreshToken.Create(
            user.Id,
            issued.RefreshTokenHash,
            user.SecurityVersion,
            issued.RefreshTokenExpiresAt,
            now,
            ipAddress,
            NormalizeDeviceName(deviceName)));

        await identityRepository.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(ToAuthenticatedUser(user), issued);
    }

    public async Task LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 2048)
        {
            return;
        }

        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var storedToken = await identityRepository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (storedToken is null)
        {
            return;
        }

        storedToken.Revoke(timeProvider.GetUtcNow(), ipAddress);
        await identityRepository.SaveChangesAsync(cancellationToken);
    }

    private static AuthenticatedUser ToAuthenticatedUser(User user) =>
        new(user.Id, user.Username, user.Email, user.SecurityVersion);

    private static string? NormalizeDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        var trimmed = deviceName.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
