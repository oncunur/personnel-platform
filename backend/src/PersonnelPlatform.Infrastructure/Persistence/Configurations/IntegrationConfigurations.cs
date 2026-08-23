using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class IntegrationConfigurationHelpers
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

public sealed class IntegrationSystemConfiguration : IEntityTypeConfiguration<IntegrationSystem>
{
    public void Configure(EntityTypeBuilder<IntegrationSystem> builder)
    {
        builder.ToTable("systems", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_systems");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SystemType).HasColumnName("system_type").HasMaxLength(30).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_system_company");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_integration_system_company_code");
    }
}

public sealed class IntegrationDeviceConfiguration : IEntityTypeConfiguration<IntegrationDevice>
{
    public void Configure(EntityTypeBuilder<IntegrationDevice> builder)
    {
        builder.ToTable("devices", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_devices");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ScopedCampId).HasColumnName("scoped_camp_id");
        builder.Property(x => x.CredentialHash).HasColumnName("credential_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.LastErrorAt).HasColumnName("last_error_at");
        builder.Property(x => x.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(1000);
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_device_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_device_system");
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.ScopedCampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_device_camp");
        builder.HasIndex(x => new { x.IntegrationSystemId, x.Code }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_integration_device_system_code");
        builder.HasIndex(x => new { x.CompanyId, x.IsActive }).HasDatabaseName("ix_integration_device_company_active");
    }
}

public sealed class ExternalEntityMappingConfiguration : IEntityTypeConfiguration<ExternalEntityMapping>
{
    public void Configure(EntityTypeBuilder<ExternalEntityMapping> builder)
    {
        builder.ToTable("entity_mappings", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_entity_mappings");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalCode).HasColumnName("external_code").HasMaxLength(200).IsRequired();
        builder.Property(x => x.InternalEntityId).HasColumnName("internal_entity_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_mapping_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_mapping_system");
        builder.HasIndex(x => new { x.IntegrationSystemId, x.EntityType, x.ExternalCode }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_integration_mapping_external");
    }
}

public sealed class IntegrationStagingRecordConfiguration : IEntityTypeConfiguration<IntegrationStagingRecord>
{
    public void Configure(EntityTypeBuilder<IntegrationStagingRecord> builder)
    {
        builder.ToTable("staging_records", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_staging_records");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.DeviceId).HasColumnName("device_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(240).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ProcessedEntityType).HasColumnName("processed_entity_type").HasMaxLength(60);
        builder.Property(x => x.ProcessedEntityId).HasColumnName("processed_entity_id");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_staging_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_staging_system");
        builder.HasOne<IntegrationDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_staging_device");
        builder.HasIndex(x => new { x.IntegrationSystemId, x.EventType, x.ExternalEventId }).IsUnique().HasDatabaseName("ux_integration_staging_external");
        builder.HasIndex(x => new { x.Status, x.NextRetryAt, x.ReceivedAt }).HasDatabaseName("ix_integration_staging_work_queue");
        builder.HasIndex(x => new { x.CompanyId, x.ReceivedAt }).HasDatabaseName("ix_integration_staging_company_time");
    }
}

public sealed class IntegrationStagingHistoryConfiguration : IEntityTypeConfiguration<IntegrationStagingHistory>
{
    public void Configure(EntityTypeBuilder<IntegrationStagingHistory> builder)
    {
        builder.ToTable("staging_history", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_staging_history");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.StagingRecordId).HasColumnName("staging_record_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.HasOne<IntegrationStagingRecord>().WithMany().HasForeignKey(x => x.StagingRecordId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_history_staging");
        builder.HasIndex(x => new { x.StagingRecordId, x.OccurredAt }).HasDatabaseName("ix_integration_history_staging_time");
    }
}
