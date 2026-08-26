using System.Security.Cryptography;
using System.Text;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public sealed class AuthService(
    IIdentityRepository identityRepository,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokenService,
    ITotpService totpService,
    ISensitiveDataProtector sensitiveDataProtector,
    AuthPolicyOptions policyOptions,
    TimeProvider timeProvider)
{
    private const string DummyPasswordHash = "pbkdf2-sha512$210000$ZHVtbXktc2FsdC0xNmJ5dGU=$Umnbcv8kiYHPAqX5lY5kK5X6bZoTgij1b9wsLH/r7WQ=";

    public async Task<AuthResult> LoginAsync(string username, string password, string? ipAddress, string? deviceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");

        var normalizedUsername = IdentityNormalizer.NormalizeUsername(username);
        var user = await identityRepository.FindUserByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _ = passwordHasher.Verify(password, DummyPasswordHash);
            return AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");
        }

        var now = timeProvider.GetUtcNow();
        if (user.IsLockedAt(now)) return AuthResult.Failure("AUTH_ACCOUNT_LOCKED", "Hesap geçici olarak kilitli.");

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            var locked = user.RegisterFailedLogin(now, policyOptions.MaxFailedLoginAttempts, policyOptions.LockoutDuration);
            await identityRepository.SaveChangesAsync(cancellationToken);
            return locked ? AuthResult.Failure("AUTH_ACCOUNT_LOCKED", "Hesap geçici olarak kilitli.") : AuthResult.Failure("AUTH_INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");
        }

        var roleCodes = await identityRepository.ListActiveRoleCodesAsync(user.Id, cancellationToken);
        if (policyOptions.RequiresMfa(roleCodes))
            return await CreateMfaChallengeAsync(user, now, ipAddress, deviceName, cancellationToken);

        user.RegisterSuccessfulLogin(now);
        var issued = tokenService.Issue(user, now, false);
        identityRepository.AddRefreshToken(RefreshToken.Create(user.Id, issued.RefreshTokenHash, user.SecurityVersion, issued.RefreshTokenExpiresAt, now, ipAddress, NormalizeDeviceName(deviceName), false));
        await identityRepository.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(ToAuthenticatedUser(user), issued);
    }

    public async Task<AuthResult> CompleteMfaAsync(string challengeToken, string code, string? ipAddress, string? deviceName, CancellationToken cancellationToken)
    {
        if (!policyOptions.MfaEnabled)
            return AuthResult.Failure("AUTH_MFA_DISABLED", "Ek doğrulama şu anda devre dışıdır.");

        if (string.IsNullOrWhiteSpace(challengeToken) || challengeToken.Length > 512 || string.IsNullOrWhiteSpace(code) || code.Length > 16)
            return AuthResult.Failure("AUTH_MFA_INVALID", "MFA doğrulaması başarısız.");

        var now = timeProvider.GetUtcNow();
        var challenge = await identityRepository.FindMfaChallengeByHashAsync(HashOpaqueToken(challengeToken), cancellationToken);
        if (challenge is null || !challenge.IsUsableAt(now)) return AuthResult.Failure("AUTH_MFA_INVALID", "MFA doğrulaması başarısız.");

        var user = await identityRepository.FindUserByIdAsync(challenge.UserId, cancellationToken);
        var credential = await identityRepository.FindMfaCredentialAsync(challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLockedAt(now) || credential is null)
            return AuthResult.Failure("AUTH_MFA_INVALID", "MFA doğrulaması başarısız.");

        string secret;
        try { secret = sensitiveDataProtector.Unprotect(credential.ProtectedSecret); }
        catch (CryptographicException) { return AuthResult.Failure("AUTH_MFA_INVALID", "MFA doğrulaması başarısız."); }

        if (!totpService.TryVerify(secret, code.Trim(), now, credential.LastAcceptedTimeStep, out var matchedStep))
        {
            challenge.RegisterFailure();
            await identityRepository.SaveChangesAsync(cancellationToken);
            return AuthResult.Failure("AUTH_MFA_INVALID", "MFA doğrulaması başarısız.");
        }

        if (challenge.Purpose == MfaChallengePurposes.Enrollment)
        {
            if (credential.IsEnabled) return AuthResult.Failure("AUTH_MFA_CHALLENGE_STALE", "MFA kurulumu değişti; tekrar giriş yapın.");
            credential.Enable(matchedStep, now);
            user.InvalidateSessions(now);
        }
        else
        {
            if (!credential.IsEnabled) return AuthResult.Failure("AUTH_MFA_ENROLLMENT_REQUIRED", "MFA kurulumu tamamlanmalıdır.");
            credential.RecordAcceptedCode(matchedStep, now);
        }

        challenge.Consume(now);
        user.RegisterSuccessfulLogin(now);
        var issued = tokenService.Issue(user, now, true);
        identityRepository.AddRefreshToken(RefreshToken.Create(user.Id, issued.RefreshTokenHash, user.SecurityVersion, issued.RefreshTokenExpiresAt, now, ipAddress, NormalizeDeviceName(deviceName), true));
        await identityRepository.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(ToAuthenticatedUser(user), issued);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, string? deviceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 2048)
            return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");

        var now = timeProvider.GetUtcNow();
        var tokenHash = tokenService.HashRefreshToken(refreshToken);
        var storedToken = await identityRepository.FindRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (storedToken is null || !storedToken.IsActiveAt(now)) return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");

        var user = await identityRepository.FindUserByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLockedAt(now) || storedToken.SecurityVersion != user.SecurityVersion)
        {
            storedToken.Revoke(now, ipAddress); await identityRepository.SaveChangesAsync(cancellationToken);
            return AuthResult.Failure("AUTH_REFRESH_TOKEN_INVALID", "Oturum yenileme anahtarı geçersiz.");
        }

        var roleCodes = await identityRepository.ListActiveRoleCodesAsync(user.Id, cancellationToken);
        if (policyOptions.RequiresMfa(roleCodes) && !storedToken.MfaVerified)
        {
            storedToken.Revoke(now, ipAddress); await identityRepository.SaveChangesAsync(cancellationToken);
            return AuthResult.Failure("AUTH_MFA_REQUIRED", "Bu rol için MFA ile yeniden giriş yapılmalıdır.");
        }

        var issued = tokenService.Issue(user, now, storedToken.MfaVerified);
        storedToken.Rotate(now, issued.RefreshTokenHash, ipAddress);
        identityRepository.AddRefreshToken(RefreshToken.Create(user.Id, issued.RefreshTokenHash, user.SecurityVersion, issued.RefreshTokenExpiresAt, now, ipAddress, NormalizeDeviceName(deviceName), storedToken.MfaVerified));
        await identityRepository.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(ToAuthenticatedUser(user), issued);
    }

    public async Task LogoutAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 2048) return;
        var storedToken = await identityRepository.FindRefreshTokenByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (storedToken is null) return;
        storedToken.Revoke(timeProvider.GetUtcNow(), ipAddress);
        await identityRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> LogoutAllAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        var user = await identityRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) return false;
        var now = timeProvider.GetUtcNow();
        foreach (var token in await identityRepository.ListActiveRefreshTokensAsync(userId, now, cancellationToken)) token.Revoke(now, ipAddress);
        user.InvalidateSessions(now);
        await identityRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AuthResult> CreateMfaChallengeAsync(User user, DateTimeOffset now, string? ipAddress, string? deviceName, CancellationToken ct)
    {
        var credential = await identityRepository.FindMfaCredentialAsync(user.Id, ct);
        var enrollment = credential is null || !credential.IsEnabled;
        string? enrollmentSecret = null;
        if (enrollment)
        {
            enrollmentSecret = totpService.GenerateSecret();
            var protectedSecret = sensitiveDataProtector.Protect(enrollmentSecret);
            if (credential is null) { credential = UserMfaCredential.CreatePending(user.Id, protectedSecret, now); identityRepository.AddMfaCredential(credential); }
            else credential.ReplacePendingSecret(protectedSecret, now);
        }

        var plaintextChallenge = GenerateOpaqueToken();
        var expiresAt = now.Add(policyOptions.MfaChallengeLifetime);
        identityRepository.AddMfaChallenge(MfaChallenge.Create(user.Id, HashOpaqueToken(plaintextChallenge), enrollment ? MfaChallengePurposes.Enrollment : MfaChallengePurposes.Login, now, expiresAt, ipAddress, NormalizeDeviceName(deviceName)));
        await identityRepository.SaveChangesAsync(ct);
        return AuthResult.MfaRequired(ToAuthenticatedUser(user), new MfaChallengeInfo(plaintextChallenge, enrollment ? MfaChallengePurposes.Enrollment : MfaChallengePurposes.Login, enrollment, enrollmentSecret, enrollmentSecret is null ? null : totpService.BuildOtpAuthUri(policyOptions.MfaIssuer, user.Username, enrollmentSecret), expiresAt));
    }

    private static AuthenticatedUser ToAuthenticatedUser(User user) => new(user.Id, user.Username, user.Email, user.SecurityVersion);
    private static string? NormalizeDeviceName(string? deviceName) { if (string.IsNullOrWhiteSpace(deviceName)) return null; var trimmed = deviceName.Trim(); return trimmed.Length <= 200 ? trimmed : trimmed[..200]; }
    private static string GenerateOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string HashOpaqueToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
