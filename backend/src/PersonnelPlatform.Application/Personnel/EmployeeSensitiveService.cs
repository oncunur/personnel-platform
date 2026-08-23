using System.Security.Cryptography;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Personnel;

public sealed class EmployeeSensitiveService(
    IEmployeeSensitiveRepository repository,
    IPersonnelRepository personnelRepository,
    AccessControlService accessControlService,
    ISensitiveDataProtector protector,
    TimeProvider timeProvider)
{
    public async Task<PersonnelResult<EmployeeSensitiveProfileSummary>> GetAsync(Guid userId, Guid employeeId, bool reveal, CancellationToken ct)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, ct);
        if (employee is null) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, ct)) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (!await accessControlService.HasPermissionAsync(userId, PersonnelPermissions.SensitiveView, ct)) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("PERMISSION_DENIED", "Hassas personel alanlarını görüntüleme yetkiniz yok.");
        if (reveal && !await accessControlService.HasPermissionAsync(userId, PersonnelPermissions.SensitiveReveal, ct)) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("SENSITIVE_REVEAL_DENIED", "Hassas alanları açık görüntüleme yetkiniz yok.");

        var row = await repository.FindAsync(employeeId, ct);
        if (row is null) return PersonnelResult<EmployeeSensitiveProfileSummary>.Success(new(employeeId, null, null, null, null, reveal, 0));
        try
        {
            var nationalId = row.NationalIdCiphertext is null ? null : protector.Unprotect(row.NationalIdCiphertext);
            var iban = row.IbanCiphertext is null ? null : protector.Unprotect(row.IbanCiphertext);
            return PersonnelResult<EmployeeSensitiveProfileSummary>.Success(new(employeeId, SensitiveDataMasking.MaskNationalId(nationalId), SensitiveDataMasking.MaskIban(iban), reveal ? nationalId : null, reveal ? iban : null, reveal, row.Version));
        }
        catch (CryptographicException)
        {
            return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("SENSITIVE_DATA_DECRYPTION_FAILED", "Hassas veri çözülemedi; güvenlik yöneticisine başvurun.");
        }
    }

    public async Task<PersonnelResult<EmployeeSensitiveProfileSummary>> UpsertAsync(Guid userId, Guid employeeId, UpsertEmployeeSensitiveProfileRequest request, CancellationToken ct)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, ct);
        if (employee is null) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, ct)) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (!await accessControlService.HasPermissionAsync(userId, PersonnelPermissions.SensitiveManage, ct)) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("PERMISSION_DENIED", "Hassas personel alanlarını değiştirme yetkiniz yok.");

        var nationalId = NormalizeNationalId(request.NationalId);
        var iban = NormalizeIban(request.Iban);
        if (request.NationalId is not null && nationalId is null) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("NATIONAL_ID_INVALID", "Kimlik numarası biçimi geçersiz.");
        if (request.Iban is not null && (iban is null || !IsValidIban(iban))) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("IBAN_INVALID", "IBAN biçimi veya kontrol basamakları geçersiz.");

        var nationalCipher = nationalId is null ? null : protector.Protect(nationalId);
        var ibanCipher = iban is null ? null : protector.Protect(iban);
        var row = await repository.FindAsync(employeeId, ct);
        var now = timeProvider.GetUtcNow();
        if (row is null)
        {
            if (request.Version is not null and not 0) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Hassas profil başka bir işlem tarafından değiştirildi.");
            row = EmployeeSensitiveProfile.Create(employee.CompanyId, employeeId, nationalCipher, ibanCipher, now, userId); repository.Add(row);
        }
        else
        {
            if (request.Version is null || row.Version != request.Version.Value) return PersonnelResult<EmployeeSensitiveProfileSummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Hassas profil başka bir kullanıcı tarafından değiştirildi.");
            row.Update(nationalCipher, ibanCipher, now, userId);
        }
        await repository.SaveChangesAsync(ct);
        return PersonnelResult<EmployeeSensitiveProfileSummary>.Success(new(employeeId, SensitiveDataMasking.MaskNationalId(nationalId), SensitiveDataMasking.MaskIban(iban), null, null, false, row.Version));
    }

    private static string? NormalizeNationalId(string? value)
    {
        if (value is null) return null;
        var v = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (v.Length is < 5 or > 50 || !v.All(c => char.IsLetterOrDigit(c) || c is '-' or '/')) return null;
        return v.ToUpperInvariant();
    }

    private static string? NormalizeIban(string? value)
    {
        if (value is null) return null;
        var v = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        return v.Length is >= 15 and <= 34 && v.All(char.IsLetterOrDigit) ? v : null;
    }

    private static bool IsValidIban(string iban)
    {
        if (iban.Length < 4 || !char.IsLetter(iban[0]) || !char.IsLetter(iban[1]) || !char.IsDigit(iban[2]) || !char.IsDigit(iban[3])) return false;
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c)) remainder = (remainder * 10 + (c - '0')) % 97;
            else
            {
                var n = c - 'A' + 10;
                remainder = (remainder * 10 + n / 10) % 97;
                remainder = (remainder * 10 + n % 10) % 97;
            }
        }
        return remainder == 1;
    }
}
