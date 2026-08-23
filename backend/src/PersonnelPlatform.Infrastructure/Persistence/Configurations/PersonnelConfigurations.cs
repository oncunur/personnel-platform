using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class EmployeeTypeConfiguration : IEntityTypeConfiguration<EmployeeType>
{
    public void Configure(EntityTypeBuilder<EmployeeType> builder)
    {
        builder.ToTable("employee_types", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employee_types");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_employee_types_code");
    }
}

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employees");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.DepartmentId).HasColumnName("department_id");
        builder.Property(x => x.PositionId).HasColumnName("position_id");
        builder.Property(x => x.EmployeeTypeId).HasColumnName("employee_type_id");
        builder.Property(x => x.ManagerEmployeeId).HasColumnName("manager_employee_id");
        builder.Property(x => x.EmployeeNo).HasColumnName("employee_no").HasMaxLength(50).IsRequired();
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PreferredName).HasColumnName("preferred_name").HasMaxLength(100);
        builder.Property(x => x.BirthDate).HasColumnName("birth_date");
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(x => x.HireDate).HasColumnName("hire_date").IsRequired();
        builder.Property(x => x.TerminationDate).HasColumnName("termination_date");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsRequired().IsConcurrencyToken();
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeNo }).IsUnique().HasDatabaseName("ux_employees_company_employee_no");
        builder.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("ix_employees_company_status");
        builder.HasIndex(x => x.DepartmentId).HasDatabaseName("ix_employees_department_id");
        builder.HasIndex(x => x.PositionId).HasDatabaseName("ix_employees_position_id");
        builder.HasIndex(x => x.EmployeeTypeId).HasDatabaseName("ix_employees_employee_type_id");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeType>().WithMany().HasForeignKey(x => x.EmployeeTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeProjectAssignmentConfiguration : IEntityTypeConfiguration<EmployeeProjectAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeProjectAssignment> builder)
    {
        builder.ToTable("employee_project_assignments", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employee_project_assignments");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.CostCenterId).HasColumnName("cost_center_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.AllocationPercent).HasColumnName("allocation_percent").HasPrecision(5, 2);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsRequired().IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmployeeId, x.ValidFrom }).HasDatabaseName("ix_employee_project_assignments_employee_date");
        builder.HasIndex(x => new { x.ProjectId, x.ValidFrom }).HasDatabaseName("ix_employee_project_assignments_project_date");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }
}
