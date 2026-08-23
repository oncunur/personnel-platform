using PersonnelPlatform.Domain.Common;

namespace PersonnelPlatform.Domain.Payroll;

public sealed class CompensationSalarySecret : Entity
{
    private CompensationSalarySecret() { }
    public Guid CompensationId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string ProtectedMonthlyBaseSalary { get; private set; } = string.Empty;
    public int KeyVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CompensationSalarySecret Create(Guid compensationId, Guid companyId, Guid employeeId, string protectedSalary, DateTimeOffset now, int keyVersion = 1)
    {
        if (compensationId == Guid.Empty || companyId == Guid.Empty || employeeId == Guid.Empty) throw new ArgumentException("Compensation, company and employee are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSalary);
        if (protectedSalary.Length > 2000) throw new ArgumentOutOfRangeException(nameof(protectedSalary));
        if (keyVersion < 1) throw new ArgumentOutOfRangeException(nameof(keyVersion));
        return new CompensationSalarySecret { CompensationId = compensationId, CompanyId = companyId, EmployeeId = employeeId, ProtectedMonthlyBaseSalary = protectedSalary, KeyVersion = keyVersion, CreatedAt = now.ToUniversalTime() };
    }
}
