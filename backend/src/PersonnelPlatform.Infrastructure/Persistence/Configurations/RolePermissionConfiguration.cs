using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_role_permissions");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoleId).HasColumnName("role_id").IsRequired();
        builder.Property(x => x.PermissionId).HasColumnName("permission_id").IsRequired();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(x => x.GrantedBy).HasColumnName("granted_by");
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("ux_role_permissions_role_permission");
        builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_role_permissions_roles_role_id");
        builder.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_role_permissions_permissions_permission_id");
    }
}
