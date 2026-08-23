using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Domain.Reporting;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class FinanceReportingConfigurationHelpers
{
    public static void Audit<T>(EntityTypeBuilder<T> b) where T : AuditableEntity
    {
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}

public sealed class CostEntryConfiguration : IEntityTypeConfiguration<CostEntry>
{
    public void Configure(EntityTypeBuilder<CostEntry> b)
    {
        b.ToTable("cost_entries", DatabaseSchemas.Finance);
        b.HasKey(x => x.Id).HasName("pk_finance_cost_entries");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(40);
        b.Property(x => x.SourceId).HasColumnName("source_id");
        b.Property(x => x.SourceLineKey).HasColumnName("source_line_key").HasMaxLength(220);
        b.Property(x => x.EmployeeId).HasColumnName("employee_id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CostCenterId).HasColumnName("cost_center_id");
        b.Property(x => x.CostDate).HasColumnName("cost_date");
        b.Property(x => x.Category).HasColumnName("category").HasMaxLength(50);
        b.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        b.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(30);
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        b.Property(x => x.AllocationBasis).HasColumnName("allocation_basis").HasMaxLength(30);
        b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_cost_company");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_cost_employee");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_cost_project");
        b.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_cost_center");
        b.HasIndex(x => new { x.SourceType, x.SourceId, x.SourceLineKey }).IsUnique().HasDatabaseName("ux_finance_cost_source_line");
        b.HasIndex(x => new { x.CompanyId, x.CostDate }).HasDatabaseName("ix_finance_cost_company_date");
        b.HasIndex(x => new { x.ProjectId, x.CostDate }).HasDatabaseName("ix_finance_cost_project_date");
    }
}

public sealed class PayrollCostAllocationOverrideConfiguration : IEntityTypeConfiguration<PayrollCostAllocationOverride>
{
    public void Configure(EntityTypeBuilder<PayrollCostAllocationOverride> b)
    {
        b.ToTable("payroll_allocation_overrides", DatabaseSchemas.Finance);
        b.HasKey(x => x.Id).HasName("pk_finance_payroll_allocation_overrides");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PayrollPeriodId).HasColumnName("payroll_period_id");
        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.EmployeeId).HasColumnName("employee_id");
        b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.CostCenterId).HasColumnName("cost_center_id");
        b.Property(x => x.AllocationPercent).HasColumnName("allocation_percent").HasPrecision(8, 4);
        FinanceReportingConfigurationHelpers.Audit(b);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_allocation_payroll_period");
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_allocation_company");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_allocation_employee");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_allocation_project");
        b.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_finance_allocation_cost_center");
        b.HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId }).HasDatabaseName("ix_finance_allocation_period_employee");
    }
}

public sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> b)
    {
        b.ToTable("export_jobs", DatabaseSchemas.Reporting);
        b.HasKey(x => x.Id).HasName("pk_reporting_export_jobs");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CompanyId).HasColumnName("company_id");
        b.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        b.Property(x => x.ReportType).HasColumnName("report_type").HasMaxLength(50);
        b.Property(x => x.Format).HasColumnName("format").HasMaxLength(10);
        b.Property(x => x.FiltersJson).HasColumnName("filters_json").HasColumnType("jsonb");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        b.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
        b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(240);
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(150);
        b.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        FinanceReportingConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_reporting_export_company");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_reporting_export_user");
        b.HasIndex(x => new { x.RequestedByUserId, x.CreatedAt }).HasDatabaseName("ix_reporting_export_user_time");
        b.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_reporting_export_status_time");
    }
}
