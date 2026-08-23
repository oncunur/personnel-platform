using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class AdministrativeTaskConfiguration : IEntityTypeConfiguration<AdministrativeTask>
{
    public void Configure(EntityTypeBuilder<AdministrativeTask> builder)
    {
        builder.ToTable("administrative_tasks", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_administrative_tasks");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(x => x.ResponsibleUserId).HasColumnName("responsible_user_id");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.RecurrenceUnit).HasColumnName("recurrence_unit").HasMaxLength(20);
        builder.Property(x => x.RecurrenceInterval).HasColumnName("recurrence_interval");
        builder.Property(x => x.ReminderDaysBefore).HasColumnName("reminder_days_before");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.CompletionCount).HasColumnName("completion_count");
        builder.Property(x => x.LastCompletedAt).HasColumnName("last_completed_at");
        builder.Property(x => x.LastCompletedBy).HasColumnName("last_completed_by");
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_tasks_company");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_tasks_responsible_user");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.LastCompletedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_tasks_last_completed_by");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_administrative_tasks_company_code_model");
        builder.HasIndex(x => new { x.CompanyId, x.Status, x.DueDate }).HasDatabaseName("ix_administrative_tasks_company_status_due");
    }
}

public sealed class AdministrativeTaskCompletionConfiguration : IEntityTypeConfiguration<AdministrativeTaskCompletion>
{
    public void Configure(EntityTypeBuilder<AdministrativeTaskCompletion> builder)
    {
        builder.ToTable("administrative_task_completions", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_administrative_task_completions");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.TaskId).HasColumnName("task_id");
        builder.Property(x => x.DueDateSnapshot).HasColumnName("due_date_snapshot");
        builder.Property(x => x.CompletedLocalDate).HasColumnName("completed_local_date");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CompletedBy).HasColumnName("completed_by");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        builder.HasOne<AdministrativeTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_task_completions_task");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_task_completions_company");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CompletedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_task_completions_user");
        builder.HasIndex(x => new { x.TaskId, x.CompletedAt }).HasDatabaseName("ix_administrative_task_completions_task_time");
    }
}

public sealed class AdministrativeContractConfiguration : IEntityTypeConfiguration<AdministrativeContract>
{
    public void Configure(EntityTypeBuilder<AdministrativeContract> builder)
    {
        builder.ToTable("administrative_contracts", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_administrative_contracts");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.ContractNo).HasColumnName("contract_no").HasMaxLength(100);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.Counterparty).HasColumnName("counterparty").HasMaxLength(200);
        builder.Property(x => x.ResponsibleUserId).HasColumnName("responsible_user_id");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.ReminderDaysBefore).HasColumnName("reminder_days_before");
        builder.Property(x => x.AutoRenewal).HasColumnName("auto_renewal");
        builder.Property(x => x.ContractValue).HasColumnName("contract_value").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_contracts_company");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_administrative_contracts_responsible_user");
        builder.HasIndex(x => new { x.CompanyId, x.ContractNo }).IsUnique().HasDatabaseName("ux_administrative_contracts_company_no_model");
        builder.HasIndex(x => new { x.CompanyId, x.Status, x.EndDate }).HasDatabaseName("ix_administrative_contracts_company_status_end");
    }
}

public sealed class AdministrativeReminderEventConfiguration : IEntityTypeConfiguration<AdministrativeReminderEvent>
{
    public void Configure(EntityTypeBuilder<AdministrativeReminderEvent> builder)
    {
        builder.ToTable("reminder_events", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_reminder_events");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80);
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50);
        builder.Property(x => x.SourceId).HasColumnName("source_id");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20);
        builder.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(300);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(1000);
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_reminder_events_company");
        builder.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("ux_reminder_events_dedupe_model");
        builder.HasIndex(x => new { x.CompanyId, x.CreatedAt }).HasDatabaseName("ix_reminder_events_company_time");
    }
}
