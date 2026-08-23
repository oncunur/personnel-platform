using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class OrganizationConfigurationHelpers
{
    public static void ConfigureBase<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : OrganizationEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsRequired().IsConcurrencyToken();
    }
}

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.TaxNumber).HasColumnName("tax_number").HasMaxLength(50);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(1000);
        builder.Property(x => x.DefaultCurrency).HasColumnName("default_currency").HasMaxLength(3).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_companies_code");
    }
}

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(1000);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_branches_company_code");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.ParentDepartmentId).HasColumnName("parent_department_id");
        builder.Property(x => x.ManagerEmployeeId).HasColumnName("manager_employee_id");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_departments_company_code");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentDepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.DepartmentId).HasColumnName("department_id");
        builder.HasIndex(x => new { x.DepartmentId, x.Code }).IsUnique().HasDatabaseName("ux_positions_department_code");
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(3);
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.PlannedEndDate).HasColumnName("planned_end_date");
        builder.Property(x => x.ActualEndDate).HasColumnName("actual_end_date");
        builder.Property(x => x.ManagerEmployeeId).HasColumnName("manager_employee_id");
        builder.Property(x => x.DefaultCostCenterId).HasColumnName("default_cost_center_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_projects_company_code");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.DefaultCostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("cost_centers", DatabaseSchemas.Organization);
        OrganizationConfigurationHelpers.ConfigureBase(builder);
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.ParentCostCenterId).HasColumnName("parent_cost_center_id");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_cost_centers_company_code");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.ParentCostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}
