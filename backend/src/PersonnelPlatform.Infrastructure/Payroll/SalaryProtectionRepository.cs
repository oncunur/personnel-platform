using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Payroll;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Payroll;

public sealed class SalaryProtectionRepository(
    ApplicationDbContext db,
    ISensitiveDataProtector protector,
    TimeProvider timeProvider) : ISalaryProtectionRepository
{
    public void StageForNewCompensation(EmployeeCompensation compensation)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        var protectedSalary = protector.Protect(compensation.MonthlyBaseSalary.ToString(CultureInfo.InvariantCulture));
        db.CompensationSalarySecrets.Add(CompensationSalarySecret.Create(
            compensation.Id,
            compensation.CompanyId,
            compensation.EmployeeId,
            protectedSalary,
            timeProvider.GetUtcNow()));
    }

    public async Task<decimal> ResolveSalaryAsync(Guid compensationId, decimal legacySalary, CancellationToken cancellationToken)
    {
        var secret = await db.CompensationSalarySecrets.AsNoTracking().FirstOrDefaultAsync(x => x.CompensationId == compensationId, cancellationToken);
        if (secret is null)
        {
            if (legacySalary > 0m) return legacySalary;
            throw new InvalidOperationException("Protected salary is missing for compensation.");
        }

        var plaintext = protector.Unprotect(secret.ProtectedMonthlyBaseSalary);
        if (!decimal.TryParse(plaintext, NumberStyles.Number, CultureInfo.InvariantCulture, out var salary) || salary <= 0m)
            throw new InvalidOperationException("Protected salary payload is invalid.");
        return salary;
    }

    public async Task<SalaryProtectionBackfillResult> BackfillLegacyAsync(int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 1000);
        var candidates = await db.EmployeeCompensations.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.MonthlyBaseSalary > 0m && !db.CompensationSalarySecrets.Any(s => s.CompensationId == x.Id))
            .OrderBy(x => x.Id)
            .Take(take)
            .Select(x => new { x.Id, x.CompanyId, x.EmployeeId, x.MonthlyBaseSalary })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return new SalaryProtectionBackfillResult(0, await CountUnprotectedAsync(cancellationToken));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in candidates)
        {
            var protectedSalary = protector.Protect(row.MonthlyBaseSalary.ToString(CultureInfo.InvariantCulture));
            db.CompensationSalarySecrets.Add(CompensationSalarySecret.Create(row.Id, row.CompanyId, row.EmployeeId, protectedSalary, timeProvider.GetUtcNow()));
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var row in candidates)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payroll.employee_compensations SET monthly_base_salary = 0 WHERE id = {row.Id}", cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new SalaryProtectionBackfillResult(candidates.Count, await CountUnprotectedAsync(cancellationToken));
    }

    public Task<int> CountUnprotectedAsync(CancellationToken cancellationToken) =>
        db.EmployeeCompensations.AsNoTracking().CountAsync(
            x => x.DeletedAt == null && (x.MonthlyBaseSalary != 0m || !db.CompensationSalarySecrets.Any(s => s.CompensationId == x.Id)),
            cancellationToken);
}
