namespace PersonnelPlatform.Application.Identity;

public interface ITotpService
{
    string GenerateSecret();
    bool TryVerify(string base32Secret, string code, DateTimeOffset now, long? lastAcceptedTimeStep, out long matchedTimeStep);
    string BuildOtpAuthUri(string issuer, string accountName, string base32Secret);
}
