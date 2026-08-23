using PersonnelPlatform.Domain.Payroll;

namespace PersonnelPlatform.Application.Payroll;

public interface ISalaryProtectionRepository
{
    void StageForNewCompensation(EmployeeCompensation compensation);
    Task<decimal> ResolveSalaryAsync(Guid compensationId, decimal legacySalary, CancellationToken cancellationToken);
    Task<SalaryProtectionBackfillResult> BackfillLegacyAsync(int take, CancellationToken cancellationToken);
    Task<int> CountUnprotectedAsync(CancellationToken cancellationToken);
}

public sealed record SalaryProtectionBackfillResult(int Processed, int Remaining);
