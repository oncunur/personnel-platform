using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class EmployeeUserLinkConfiguration : IEntityTypeConfiguration<EmployeeUserLink>
{
    public void Configure(EntityTypeBuilder<EmployeeUserLink> builder)
    {
        builder.ToTable("user_employee_links", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_user_employee_links");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_employee_links_user");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_employee_links_employee");
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_user_employee_links_user");
        builder.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("is_active = TRUE AND deleted_at IS NULL").HasDatabaseName("ux_user_employee_links_employee_active");
    }
}

public sealed class LeaveApprovalConfiguration : IEntityTypeConfiguration<LeaveApproval>
{
    public void Configure(EntityTypeBuilder<LeaveApproval> builder)
    {
        builder.ToTable("leave_approvals", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_approvals");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveId).HasColumnName("leave_id");
        builder.Property(x => x.StepOrder).HasColumnName("step_order");
        builder.Property(x => x.StepCode).HasColumnName("step_code").HasMaxLength(30);
        builder.Property(x => x.ApproverEmployeeId).HasColumnName("approver_employee_id");
        builder.Property(x => x.AssignedUserId).HasColumnName("assigned_user_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(x => x.DecidedAt).HasColumnName("decided_at");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(1000);
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_leave_approvals_leave");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.ApproverEmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_approvals_approver_employee");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_approvals_assigned_user");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_approvals_decided_user");
        builder.HasIndex(x => new { x.LeaveId, x.StepOrder }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_leave_approvals_leave_step");
        builder.HasIndex(x => new { x.Status, x.StepCode }).HasDatabaseName("ix_leave_approvals_status_step");
    }
}

public sealed class LeaveApprovalHistoryConfiguration : IEntityTypeConfiguration<LeaveApprovalHistory>
{
    public void Configure(EntityTypeBuilder<LeaveApprovalHistory> builder)
    {
        builder.ToTable("leave_approval_history", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_approval_history");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveId).HasColumnName("leave_id");
        builder.Property(x => x.ApprovalId).HasColumnName("approval_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
        builder.Property(x => x.StepCode).HasColumnName("step_code").HasMaxLength(30);
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30);
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_leave_approval_history_leave");
        builder.HasOne<LeaveApproval>().WithMany().HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_leave_approval_history_approval");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_approval_history_actor");
        builder.HasIndex(x => new { x.LeaveId, x.OccurredAt }).HasDatabaseName("ix_leave_approval_history_leave_occurred");
    }
}
