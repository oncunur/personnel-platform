using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class EmployeeSensitiveProfileConfiguration : IEntityTypeConfiguration<EmployeeSensitiveProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeSensitiveProfile> builder)
    {
        builder.ToTable("employee_sensitive_profiles", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employee_sensitive_profiles");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.NationalIdCiphertext).HasColumnName("national_id_ciphertext").HasMaxLength(2000);
        builder.Property(x => x.IbanCiphertext).HasColumnName("iban_ciphertext").HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_employee_sensitive_company");
        builder.HasOne<Employee>().WithOne().HasForeignKey<EmployeeSensitiveProfile>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_employee_sensitive_employee");
        builder.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_employee_sensitive_employee");
    }
}
