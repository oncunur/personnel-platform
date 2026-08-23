using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Integration;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_import_jobs");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(240).IsRequired();
        builder.Property(x => x.FileHash).HasColumnName("file_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.HeadersJson).HasColumnName("headers_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.MappingJson).HasColumnName("mapping_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TotalRows).HasColumnName("total_rows");
        builder.Property(x => x.SuccessRows).HasColumnName("success_rows");
        builder.Property(x => x.ErrorRows).HasColumnName("error_rows");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_import_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_import_system");
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt }).HasDatabaseName("ix_integration_import_company_time");
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_integration_import_status_time");
    }
}

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("import_rows", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_import_rows");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ImportJobId).HasColumnName("import_job_id");
        builder.Property(x => x.RowNumber).HasColumnName("row_number");
        builder.Property(x => x.RawJson).HasColumnName("raw_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ProcessedEntityType).HasColumnName("processed_entity_type").HasMaxLength(60);
        builder.Property(x => x.ProcessedEntityId).HasColumnName("processed_entity_id");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.HasOne<ImportJob>().WithMany().HasForeignKey(x => x.ImportJobId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_import_row_job");
        builder.HasIndex(x => new { x.ImportJobId, x.RowNumber }).IsUnique().HasDatabaseName("ux_integration_import_row_number");
        builder.HasIndex(x => new { x.ImportJobId, x.Status }).HasDatabaseName("ix_integration_import_row_status");
    }
}

public sealed class ErpAccountMappingConfiguration : IEntityTypeConfiguration<ErpAccountMapping>
{
    public void Configure(EntityTypeBuilder<ErpAccountMapping> builder)
    {
        builder.ToTable("erp_account_mappings", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_erp_account_mappings");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.CostCategory).HasColumnName("cost_category").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountCode).HasColumnName("account_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CounterAccountCode).HasColumnName("counter_account_code").HasMaxLength(100);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_account_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_account_system");
        builder.HasIndex(x => new { x.IntegrationSystemId, x.CostCategory }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_integration_erp_account_category");
    }
}

public sealed class ErpExportBatchConfiguration : IEntityTypeConfiguration<ErpExportBatch>
{
    public void Configure(EntityTypeBuilder<ErpExportBatch> builder)
    {
        builder.ToTable("erp_export_batches", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_erp_export_batches");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.IntegrationSystemId).HasColumnName("integration_system_id");
        builder.Property(x => x.FromDate).HasColumnName("from_date");
        builder.Property(x => x.ToDate).HasColumnName("to_date");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.ReconciledAt).HasColumnName("reconciled_at");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        IntegrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_batch_company");
        builder.HasOne<IntegrationSystem>().WithMany().HasForeignKey(x => x.IntegrationSystemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_batch_system");
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt }).HasDatabaseName("ix_integration_erp_batch_company_time");
        builder.HasIndex(x => new { x.IntegrationSystemId, x.Status }).HasDatabaseName("ix_integration_erp_batch_system_status");
    }
}

public sealed class ErpExportLineConfiguration : IEntityTypeConfiguration<ErpExportLine>
{
    public void Configure(EntityTypeBuilder<ErpExportLine> builder)
    {
        builder.ToTable("erp_export_lines", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_erp_export_lines");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.CostEntryId).HasColumnName("cost_entry_id");
        builder.Property(x => x.ExternalLineKey).HasColumnName("external_line_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.CostCenterId).HasColumnName("cost_center_id");
        builder.Property(x => x.CostDate).HasColumnName("cost_date");
        builder.Property(x => x.CostCategory).HasColumnName("cost_category").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountCode).HasColumnName("account_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CounterAccountCode).HasColumnName("counter_account_code").HasMaxLength(100);
        builder.Property(x => x.SentAmount).HasColumnName("sent_amount").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.AcceptedAmount).HasColumnName("accepted_amount").HasPrecision(18, 2);
        builder.Property(x => x.VarianceAmount).HasColumnName("variance_amount").HasPrecision(18, 2);
        builder.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(200);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ReconciledAt).HasColumnName("reconciled_at");
        builder.HasOne<ErpExportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_line_batch");
        builder.HasOne<CostEntry>().WithMany().HasForeignKey(x => x.CostEntryId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_line_cost_entry");
        builder.HasIndex(x => new { x.BatchId, x.CostEntryId }).IsUnique().HasDatabaseName("ux_integration_erp_line_cost");
        builder.HasIndex(x => new { x.BatchId, x.ExternalLineKey }).IsUnique().HasDatabaseName("ux_integration_erp_line_external");
        builder.HasIndex(x => new { x.CostEntryId, x.Status }).HasDatabaseName("ix_integration_erp_line_cost_status");
    }
}

public sealed class ErpReconciliationEventConfiguration : IEntityTypeConfiguration<ErpReconciliationEvent>
{
    public void Configure(EntityTypeBuilder<ErpReconciliationEvent> builder)
    {
        builder.ToTable("erp_reconciliation_events", DatabaseSchemas.Integration);
        builder.HasKey(x => x.Id).HasName("pk_integration_erp_reconciliation_events");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BatchId).HasColumnName("batch_id");
        builder.Property(x => x.LineId).HasColumnName("line_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SentAmount).HasColumnName("sent_amount").HasPrecision(18, 2);
        builder.Property(x => x.AcceptedAmount).HasColumnName("accepted_amount").HasPrecision(18, 2);
        builder.Property(x => x.VarianceAmount).HasColumnName("variance_amount").HasPrecision(18, 2);
        builder.Property(x => x.ExternalReference).HasColumnName("external_reference").HasMaxLength(200);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.HasOne<ErpExportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_recon_batch");
        builder.HasOne<ErpExportLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_integration_erp_recon_line");
        builder.HasIndex(x => new { x.BatchId, x.OccurredAt }).HasDatabaseName("ix_integration_erp_recon_batch_time");
    }
}
