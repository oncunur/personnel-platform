using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Migration;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class MigrationRunConfiguration : IEntityTypeConfiguration<MigrationRun>
{
    public void Configure(EntityTypeBuilder<MigrationRun> builder)
    {
        builder.ToTable("runs", DatabaseSchemas.Migration);
        builder.HasKey(x => x.Id).HasName("pk_migration_runs");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.SourceSystem).HasColumnName("source_system").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceObject).HasColumnName("source_object").HasMaxLength(150).IsRequired();
        builder.Property(x => x.TargetEntity).HasColumnName("target_entity").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceFileName).HasColumnName("source_file_name").HasMaxLength(260).IsRequired();
        builder.Property(x => x.SourceContentHash).HasColumnName("source_content_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.MappingHash).HasColumnName("mapping_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.TotalRows).HasColumnName("total_rows");
        builder.Property(x => x.ValidRows).HasColumnName("valid_rows");
        builder.Property(x => x.WarningRows).HasColumnName("warning_rows");
        builder.Property(x => x.ErrorRows).HasColumnName("error_rows");
        builder.Property(x => x.DuplicateRows).HasColumnName("duplicate_rows");
        builder.Property(x => x.ReconciliationMismatchCount).HasColumnName("reconciliation_mismatch_count");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_run_company");
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt }).HasDatabaseName("ix_migration_run_company_time");
        builder.HasIndex(x => new { x.CompanyId, x.SourceSystem, x.SourceObject, x.TargetEntity }).HasDatabaseName("ix_migration_run_source_target");
        builder.HasIndex(x => new { x.CompanyId, x.SourceContentHash, x.MappingHash }).HasDatabaseName("ix_migration_run_content_mapping");
    }
}

public sealed class MigrationStageRowConfiguration : IEntityTypeConfiguration<MigrationStageRow>
{
    public void Configure(EntityTypeBuilder<MigrationStageRow> builder)
    {
        builder.ToTable("stage_rows", DatabaseSchemas.Migration);
        builder.HasKey(x => x.Id).HasName("pk_migration_stage_rows");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MigrationRunId).HasColumnName("migration_run_id");
        builder.Property(x => x.RowNumber).HasColumnName("row_number");
        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(240).IsRequired();
        builder.Property(x => x.LineageKey).HasColumnName("lineage_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.SourceRowHash).HasColumnName("source_row_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.SourcePayloadCiphertext).HasColumnName("source_payload_ciphertext").HasColumnType("text").IsRequired();
        builder.Property(x => x.TransformedPayloadCiphertext).HasColumnName("transformed_payload_ciphertext").HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.IdempotenceStatus).HasColumnName("idempotence_status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.WarningCode).HasColumnName("warning_code").HasMaxLength(120);
        builder.Property(x => x.WarningMessage).HasColumnName("warning_message").HasMaxLength(2000);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.PreviousRunId).HasColumnName("previous_run_id");
        builder.Property(x => x.PreviousStageRowId).HasColumnName("previous_stage_row_id");
        builder.Property(x => x.StagedAt).HasColumnName("staged_at");
        builder.HasOne<MigrationRun>().WithMany().HasForeignKey(x => x.MigrationRunId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_stage_row_run");
        builder.HasIndex(x => new { x.MigrationRunId, x.RowNumber }).IsUnique().HasDatabaseName("ux_migration_stage_row_number");
        builder.HasIndex(x => new { x.MigrationRunId, x.SourceKey }).IsUnique().HasDatabaseName("ux_migration_stage_row_source_key");
        builder.HasIndex(x => new { x.MigrationRunId, x.Status }).HasDatabaseName("ix_migration_stage_row_status");
        builder.HasIndex(x => x.LineageKey).HasDatabaseName("ix_migration_stage_row_lineage");
    }
}

public sealed class MigrationLineageRecordConfiguration : IEntityTypeConfiguration<MigrationLineageRecord>
{
    public void Configure(EntityTypeBuilder<MigrationLineageRecord> builder)
    {
        builder.ToTable("lineage_records", DatabaseSchemas.Migration);
        builder.HasKey(x => x.Id).HasName("pk_migration_lineage_records");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.SourceSystem).HasColumnName("source_system").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceObject).HasColumnName("source_object").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(240).IsRequired();
        builder.Property(x => x.TargetEntity).HasColumnName("target_entity").HasMaxLength(150).IsRequired();
        builder.Property(x => x.LastSourceRowHash).HasColumnName("last_source_row_hash").HasColumnType("char(64)").IsRequired();
        builder.Property(x => x.LastRunId).HasColumnName("last_run_id");
        builder.Property(x => x.LastStageRowId).HasColumnName("last_stage_row_id");
        builder.Property(x => x.TargetEntityId).HasColumnName("target_entity_id");
        builder.Property(x => x.SeenCount).HasColumnName("seen_count");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_lineage_company");
        builder.HasOne<MigrationRun>().WithMany().HasForeignKey(x => x.LastRunId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_lineage_last_run");
        builder.HasOne<MigrationStageRow>().WithMany().HasForeignKey(x => x.LastStageRowId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_lineage_last_stage_row");
        builder.HasIndex(x => new { x.CompanyId, x.SourceSystem, x.SourceObject, x.SourceKey, x.TargetEntity }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_migration_lineage_source_target");
        builder.HasIndex(x => new { x.TargetEntity, x.TargetEntityId }).HasDatabaseName("ix_migration_lineage_target");
    }
}

public sealed class MigrationReconciliationConfiguration : IEntityTypeConfiguration<MigrationReconciliation>
{
    public void Configure(EntityTypeBuilder<MigrationReconciliation> builder)
    {
        builder.ToTable("reconciliations", DatabaseSchemas.Migration);
        builder.HasKey(x => x.Id).HasName("pk_migration_reconciliations");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MigrationRunId).HasColumnName("migration_run_id");
        builder.Property(x => x.MetricCode).HasColumnName("metric_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.MetricName).HasColumnName("metric_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceValue).HasColumnName("source_value").HasPrecision(20, 4);
        builder.Property(x => x.TargetValue).HasColumnName("target_value").HasPrecision(20, 4);
        builder.Property(x => x.Difference).HasColumnName("difference").HasPrecision(20, 4);
        builder.Property(x => x.Tolerance).HasColumnName("tolerance").HasPrecision(20, 4);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.HasOne<MigrationRun>().WithMany().HasForeignKey(x => x.MigrationRunId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_migration_reconciliation_run");
        builder.HasIndex(x => new { x.MigrationRunId, x.MetricCode }).IsUnique().HasDatabaseName("ux_migration_reconciliation_metric");
        builder.HasIndex(x => new { x.MigrationRunId, x.Status }).HasDatabaseName("ix_migration_reconciliation_status");
    }
}