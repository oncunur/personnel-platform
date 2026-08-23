using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class UserMfaCredentialConfiguration : IEntityTypeConfiguration<UserMfaCredential>
{
    public void Configure(EntityTypeBuilder<UserMfaCredential> builder)
    {
        builder.ToTable("user_mfa_credentials", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_user_mfa_credentials");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.ProtectedSecret).HasColumnName("protected_secret").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(x => x.EnabledAt).HasColumnName("enabled_at");
        builder.Property(x => x.LastAcceptedTimeStep).HasColumnName("last_accepted_time_step");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasOne<User>().WithOne().HasForeignKey<UserMfaCredential>(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_mfa_credential_user");
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_user_mfa_credential_user");
    }
}

public sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("mfa_challenges", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_mfa_challenges");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(x => x.FailedAttemptCount).HasColumnName("failed_attempt_count").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(100);
        builder.Property(x => x.DeviceName).HasColumnName("device_name").HasMaxLength(200);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mfa_challenge_user");
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_mfa_challenge_token_hash");
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt }).HasDatabaseName("ix_mfa_challenge_user_expiry");
    }
}
