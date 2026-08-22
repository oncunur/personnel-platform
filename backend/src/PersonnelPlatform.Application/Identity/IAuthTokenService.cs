using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Application.Identity;

public interface IAuthTokenService
{
    IssuedTokenPair Issue(User user, DateTimeOffset now);
    string HashRefreshToken(string refreshToken);
}
