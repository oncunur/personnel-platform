using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class PayrollConfigurationHelpers
{
    public static void ConfigureAudit<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}

public sealed class EmployeeCompensationConfiguration : IEntityTypeConfiguration<EmployeeCompensation>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensation> builder)
    {
        builder.ToTable("employee_compensations", DatabaseSchemas.Payroll);
        builder.HasKey(x => x.Id).HasName("pk_employee_compensations");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntilExclusive).HasColumnName("valid_until_exclusive");
        builder.Property(x => x.MonthlyBaseSalary).HasColumnName("monthly_base_salary").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.OvertimeMultiplier).HasColumnName("overtime_multiplier").HasPrecision(8, 4);
        PayrollConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_employee_compensations_company");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_employee_compensations_employee");
        builder.HasIndex(x => new { x.EmployeeId, x.ValidFrom }).HasDatabaseName("ix_employee_compensations_employee_from");
    }
}

public sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("periods", DatabaseSchemas.Payroll);
        builder.HasKey(x => x.Id).HasName("pk_payroll_periods");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Year).HasColumnName("year");
        builder.Property(x => x.Month).HasColumnName("month");
        builder.Property(x => x.Revision).HasColumnName("revision");
        builder.Property(x => x.PreviousRevisionId).HasColumnName("previous_revision_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.CalculationVersion).HasColumnName("calculation_version").HasMaxLength(80);
        builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
        builder.Property(x => x.CalculatedBy).HasColumnName("calculated_by");
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
        builder.Ignore(x => x.PeriodStart);
        builder.Ignore(x => x.PeriodEndExclusive);
        PayrollConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_periods_company");
        builder.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PreviousRevisionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_periods_previous_revision");
        builder.HasIndex(x => new { x.CompanyId, x.Year, x.Month, x.Revision }).IsUnique().HasDatabaseName("ux_payroll_period_revision");
        builder.HasIndex(x => new { x.CompanyId, x.Status, x.Year, x.Month }).HasDatabaseName("ix_payroll_periods_company_status");
    }
}

public sealed class PayrollEmployeeResultConfiguration : IEntityTypeConfiguration<PayrollEmployeeResult>
{
    public void Configure(EntityTypeBuilder<PayrollEmployeeResult> builder)
    {
        builder.ToTable("employee_results", DatabaseSchemas.Payroll);
        builder.HasKey(x => x.Id).HasName("pk_payroll_employee_results");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.CompensationId).HasColumnName("compensation_id");
        builder.Property(x => x.MonthlyBaseSalarySnapshot).HasColumnName("monthly_base_salary_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.CurrencySnapshot).HasColumnName("currency_snapshot").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.OvertimeMultiplierSnapshot).HasColumnName("overtime_multiplier_snapshot").HasPrecision(8, 4);
        builder.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes");
        builder.Property(x => x.WorkedMinutes).HasColumnName("worked_minutes");
        builder.Property(x => x.PaidLeaveMinutes).HasColumnName("paid_leave_minutes");
        builder.Property(x => x.ApprovedOvertimeMinutes).HasColumnName("approved_overtime_minutes");
        builder.Property(x => x.BaseSalaryAmount).HasColumnName("base_salary_amount").HasPrecision(18, 2);
        builder.Property(x => x.AbsenceDeductionAmount).HasColumnName("absence_deduction_amount").HasPrecision(18, 2);
        builder.Property(x => x.OvertimeEarningAmount).HasColumnName("overtime_earning_amount").HasPrecision(18, 2);
        builder.Property(x => x.PayBeforeStatutory).HasColumnName("pay_before_statutory").HasPrecision(18, 2);
        builder.Property(x => x.MealEmployerCost).HasColumnName("meal_employer_cost").HasPrecision(18, 2);
        builder.Property(x => x.AccommodationEmployerCost).HasColumnName("accommodation_employer_cost").HasPrecision(18, 2);
        builder.Property(x => x.EmployerCostBeforeStatutory).HasColumnName("employer_cost_before_statutory").HasPrecision(18, 2);
        builder.Property(x => x.SourceSnapshotJson).HasColumnName("source_snapshot_json").HasColumnType("jsonb");
        builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
        builder.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_employee_results_period");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_employee_results_company");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_employee_results_employee");
        builder.HasOne<EmployeeCompensation>().WithMany().HasForeignKey(x => x.CompensationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_payroll_employee_results_compensation");
        builder.HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId }).IsUnique().HasDatabaseName("ux_payroll_results_period_employee");
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId }).HasDatabaseName("ix_payroll_results_company_employee");
    }
}
