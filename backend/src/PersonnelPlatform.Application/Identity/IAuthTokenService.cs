using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public interface IAuthTokenService
{
    IssuedTokenPair Issue(User user, DateTimeOffset now, bool mfaVerified = false);
    string HashRefreshToken(string refreshToken);
}
