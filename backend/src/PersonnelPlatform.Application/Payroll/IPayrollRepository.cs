using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Payroll;

public interface IPayrollRepository
{
    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<EmployeeCompensation?> FindCompensationAsync(Guid compensationId, CancellationToken cancellationToken);
    Task<EmployeeCompensation?> FindOverlappingCompensationAsync(Guid employeeId, DateOnly validFrom, DateOnly? validUntilExclusive, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeCompensationSummary>> ListCompensationsAsync(Guid employeeId, CancellationToken cancellationToken);
    void AddCompensation(EmployeeCompensation compensation);
    Task<int> BackfillLegacySalarySecretsAsync(int take, CancellationToken cancellationToken);
    Task<int> CountUnprotectedSalaryRowsAsync(CancellationToken cancellationToken);

    Task<PayrollPeriod?> FindPeriodAsync(Guid periodId, CancellationToken cancellationToken);
    Task<PayrollPeriod?> FindLatestPeriodAsync(Guid companyId, int year, int month, CancellationToken cancellationToken);
    Task<IReadOnlyList<PayrollPeriodSummary>> ListPeriodsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, int? year, CancellationToken cancellationToken);
    void AddPeriod(PayrollPeriod period);

    Task<IReadOnlyList<PayrollCalculationSource>> BuildCalculationSourcesAsync(Guid companyId, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken);
    Task<IReadOnlyList<PayrollEmployeeResultSummary>> ListResultsAsync(Guid periodId, CancellationToken cancellationToken);
    void AddResult(PayrollEmployeeResult result);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
