using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_users");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count").IsRequired();
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(x => x.SecurityVersion).HasColumnName("security_version").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsRequired().IsConcurrencyToken();

        builder.HasIndex(x => x.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_username");

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasFilter("\"normalized_email\" IS NOT NULL")
            .HasDatabaseName("ux_users_normalized_email");
    }
}
