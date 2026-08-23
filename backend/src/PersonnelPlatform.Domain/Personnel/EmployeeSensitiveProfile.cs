using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Personnel;

public sealed class EmployeeSensitiveProfile : AuditableEntity
{
    private EmployeeSensitiveProfile() { }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string? NationalIdCiphertext { get; private set; }
    public string? IbanCiphertext { get; private set; }

    public static EmployeeSensitiveProfile Create(Guid companyId, Guid employeeId, string? nationalIdCiphertext, string? ibanCiphertext, DateTimeOffset now, Guid actorUserId)
    {
        if (companyId == Guid.Empty || employeeId == Guid.Empty || actorUserId == Guid.Empty) throw new ArgumentException("Company, employee and actor are required.");
        return new EmployeeSensitiveProfile
        {
            CompanyId = companyId,
            EmployeeId = employeeId,
            NationalIdCiphertext = NormalizeCipher(nationalIdCiphertext),
            IbanCiphertext = NormalizeCipher(ibanCiphertext),
            CreatedAt = now.ToUniversalTime(),
            CreatedBy = actorUserId
        };
    }

    public void Update(string? nationalIdCiphertext, string? ibanCiphertext, DateTimeOffset now, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorUserId));
        NationalIdCiphertext = NormalizeCipher(nationalIdCiphertext);
        IbanCiphertext = NormalizeCipher(ibanCiphertext);
        UpdatedAt = now.ToUniversalTime(); UpdatedBy = actorUserId; Version++;
    }

    private static string? NormalizeCipher(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 2000) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }
}
