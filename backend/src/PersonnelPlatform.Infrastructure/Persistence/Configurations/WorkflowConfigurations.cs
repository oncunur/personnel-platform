using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Domain.Workflow;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class WorkflowConfigurationHelpers
{
    public static void Audit<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
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

public sealed class WorkflowRequestTypeConfiguration : IEntityTypeConfiguration<WorkflowRequestType>
{
    public void Configure(EntityTypeBuilder<WorkflowRequestType> b)
    {
        b.ToTable("request_types", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_request_types");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(80); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000); b.Property(x => x.SlaMinutes).HasColumnName("sla_minutes"); b.Property(x => x.RequiredFieldsJson).HasColumnName("required_fields_json").HasColumnType("jsonb"); b.Property(x => x.IsActive).HasColumnName("is_active"); WorkflowConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_request_types_company");
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_workflow_request_types_company_code");
    }
}

public sealed class WorkflowApprovalStepDefinitionConfiguration : IEntityTypeConfiguration<WorkflowApprovalStepDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowApprovalStepDefinition> b)
    {
        b.ToTable("approval_step_definitions", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_step_definitions");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.RequestTypeId).HasColumnName("request_type_id"); b.Property(x => x.StepOrder).HasColumnName("step_order"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150); b.Property(x => x.TargetKind).HasColumnName("target_kind").HasMaxLength(20); b.Property(x => x.ApproverUserId).HasColumnName("approver_user_id"); b.Property(x => x.ApproverRoleId).HasColumnName("approver_role_id"); WorkflowConfigurationHelpers.Audit(b);
        b.HasOne<WorkflowRequestType>().WithMany().HasForeignKey(x => x.RequestTypeId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_workflow_steps_request_type"); b.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_steps_user"); b.HasOne<Role>().WithMany().HasForeignKey(x => x.ApproverRoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_steps_role");
        b.HasIndex(x => new { x.RequestTypeId, x.StepOrder }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_workflow_steps_type_order");
    }
}

public sealed class WorkflowRequestConfiguration : IEntityTypeConfiguration<WorkflowRequest>
{
    public void Configure(EntityTypeBuilder<WorkflowRequest> b)
    {
        b.ToTable("requests", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_requests");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.RequestNo).HasColumnName("request_no").HasMaxLength(40); b.Property(x => x.RequestTypeId).HasColumnName("request_type_id"); b.Property(x => x.RequesterUserId).HasColumnName("requester_user_id"); b.Property(x => x.EmployeeId).HasColumnName("employee_id"); b.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20); b.Property(x => x.RequestDataJson).HasColumnName("request_data_json").HasColumnType("jsonb"); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(30); b.Property(x => x.CurrentStepOrder).HasColumnName("current_step_order"); b.Property(x => x.SlaMinutesSnapshot).HasColumnName("sla_minutes_snapshot"); b.Property(x => x.SubmittedAt).HasColumnName("submitted_at"); b.Property(x => x.DueAt).HasColumnName("due_at"); b.Property(x => x.ResolvedAt).HasColumnName("resolved_at"); WorkflowConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_requests_company"); b.HasOne<WorkflowRequestType>().WithMany().HasForeignKey(x => x.RequestTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_requests_type"); b.HasOne<User>().WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_requests_requester"); b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_requests_employee");
        b.HasIndex(x => new { x.CompanyId, x.RequestNo }).IsUnique().HasDatabaseName("ux_workflow_requests_company_no"); b.HasIndex(x => new { x.CompanyId, x.Status, x.CreatedAt }).HasDatabaseName("ix_workflow_requests_company_status_time"); b.HasIndex(x => new { x.EmployeeId, x.CreatedAt }).HasDatabaseName("ix_workflow_requests_employee_time");
    }
}

public sealed class WorkflowRequestApprovalConfiguration : IEntityTypeConfiguration<WorkflowRequestApproval>
{
    public void Configure(EntityTypeBuilder<WorkflowRequestApproval> b)
    {
        b.ToTable("request_approvals", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_request_approvals");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.RequestId).HasColumnName("request_id"); b.Property(x => x.StepOrder).HasColumnName("step_order"); b.Property(x => x.StepNameSnapshot).HasColumnName("step_name_snapshot").HasMaxLength(150); b.Property(x => x.TargetKindSnapshot).HasColumnName("target_kind_snapshot").HasMaxLength(20); b.Property(x => x.ApproverUserIdSnapshot).HasColumnName("approver_user_id_snapshot"); b.Property(x => x.ApproverRoleIdSnapshot).HasColumnName("approver_role_id_snapshot"); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20); b.Property(x => x.ActionByUserId).HasColumnName("action_by_user_id"); b.Property(x => x.ActionAt).HasColumnName("action_at"); b.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        b.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_approvals_request"); b.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_approvals_target_user"); b.HasOne<Role>().WithMany().HasForeignKey(x => x.ApproverRoleIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_approvals_target_role"); b.HasOne<User>().WithMany().HasForeignKey(x => x.ActionByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_approvals_action_user");
        b.HasIndex(x => new { x.RequestId, x.StepOrder }).IsUnique().HasDatabaseName("ux_workflow_approvals_request_step"); b.HasIndex(x => new { x.Status, x.ApproverUserIdSnapshot }).HasDatabaseName("ix_workflow_approvals_user_pending"); b.HasIndex(x => new { x.Status, x.ApproverRoleIdSnapshot }).HasDatabaseName("ix_workflow_approvals_role_pending");
    }
}

public sealed class WorkflowRequestHistoryConfiguration : IEntityTypeConfiguration<WorkflowRequestHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowRequestHistory> b)
    {
        b.ToTable("request_history", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_request_history");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.RequestId).HasColumnName("request_id"); b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); b.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30); b.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30); b.Property(x => x.ActorUserId).HasColumnName("actor_user_id"); b.Property(x => x.OccurredAt).HasColumnName("occurred_at"); b.Property(x => x.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb");
        b.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_history_request"); b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_history_actor"); b.HasIndex(x => new { x.RequestId, x.OccurredAt }).HasDatabaseName("ix_workflow_history_request_time");
    }
}

public sealed class WorkflowSlaEventConfiguration : IEntityTypeConfiguration<WorkflowSlaEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowSlaEvent> b)
    {
        b.ToTable("sla_events", DatabaseSchemas.Workflow); b.HasKey(x => x.Id).HasName("pk_workflow_sla_events");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.RequestId).HasColumnName("request_id"); b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); b.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20); b.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(300); b.Property(x => x.Message).HasColumnName("message").HasMaxLength(1000); b.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_workflow_sla_request"); b.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("ux_workflow_sla_dedupe"); b.HasIndex(x => new { x.CompanyId, x.CreatedAt }).HasDatabaseName("ix_workflow_sla_company_time");
    }
}
