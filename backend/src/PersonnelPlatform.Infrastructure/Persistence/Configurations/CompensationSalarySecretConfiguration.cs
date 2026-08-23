using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class CompensationSalarySecretConfiguration : IEntityTypeConfiguration<CompensationSalarySecret>
{
    public void Configure(EntityTypeBuilder<CompensationSalarySecret> builder)
    {
        builder.ToTable("compensation_salary_secrets", DatabaseSchemas.Payroll);
        builder.HasKey(x => x.Id).HasName("pk_compensation_salary_secrets");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompensationId).HasColumnName("compensation_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.ProtectedMonthlyBaseSalary).HasColumnName("protected_monthly_base_salary").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.KeyVersion).HasColumnName("key_version").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne<EmployeeCompensation>().WithOne().HasForeignKey<CompensationSalarySecret>(x => x.CompensationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_compensation_salary_secret_compensation");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_compensation_salary_secret_company");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_compensation_salary_secret_employee");
        builder.HasIndex(x => x.CompensationId).IsUnique().HasDatabaseName("ux_compensation_salary_secret_compensation");
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId }).HasDatabaseName("ix_compensation_salary_secret_company_employee");
    }
}
