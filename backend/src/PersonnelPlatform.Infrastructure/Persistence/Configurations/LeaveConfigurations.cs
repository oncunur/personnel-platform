using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("leave_types", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_types");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.IsPaid).HasColumnName("is_paid");
        builder.Property(x => x.BalanceRequired).HasColumnName("balance_required");
        builder.Property(x => x.AllowHalfDay).HasColumnName("allow_half_day");
        builder.Property(x => x.AttachmentRequired).HasColumnName("attachment_required");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");
        ConfigureAudit(builder);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_leave_types_code");
    }

    internal static void ConfigureAudit<T>(EntityTypeBuilder<T> builder) where T : PersonnelPlatform.Domain.Common.AuditableEntity
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

public sealed class LeaveEntitlementConfiguration : IEntityTypeConfiguration<LeaveEntitlement>
{
    public void Configure(EntityTypeBuilder<LeaveEntitlement> builder)
    {
        builder.ToTable("leave_entitlements", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_entitlements");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id");
        builder.Property(x => x.PeriodStart).HasColumnName("period_start");
        builder.Property(x => x.PeriodEnd).HasColumnName("period_end");
        builder.Property(x => x.EntitledDays).HasColumnName("entitled_days").HasPrecision(10, 2);
        builder.Property(x => x.CarryOverDays).HasColumnName("carry_over_days").HasPrecision(10, 2);
        builder.Property(x => x.AdjustmentDays).HasColumnName("adjustment_days").HasPrecision(10, 2);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_entitlements_employee");
        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_entitlements_type");
        builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.PeriodStart, x.PeriodEnd }).IsUnique().HasDatabaseName("ux_leave_entitlements_period");
    }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("leave_balances", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_balances");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id");
        builder.Property(x => x.PeriodStart).HasColumnName("period_start");
        builder.Property(x => x.PeriodEnd).HasColumnName("period_end");
        builder.Property(x => x.EntitledDays).HasColumnName("entitled_days").HasPrecision(10, 2);
        builder.Property(x => x.CarryOverDays).HasColumnName("carry_over_days").HasPrecision(10, 2);
        builder.Property(x => x.AdjustmentDays).HasColumnName("adjustment_days").HasPrecision(10, 2);
        builder.Property(x => x.ReservedDays).HasColumnName("reserved_days").HasPrecision(10, 2);
        builder.Property(x => x.UsedDays).HasColumnName("used_days").HasPrecision(10, 2);
        builder.Ignore(x => x.AvailableDays);
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_balances_employee");
        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_balances_type");
        builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.PeriodStart, x.PeriodEnd }).IsUnique().HasDatabaseName("ux_leave_balances_period");
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leaves", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leaves");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.StartDayPart).HasColumnName("start_day_part").HasMaxLength(20);
        builder.Property(x => x.EndDayPart).HasColumnName("end_day_part").HasMaxLength(20);
        builder.Property(x => x.RequestedDays).HasColumnName("requested_days").HasPrecision(10, 2);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(x => x.WithdrawnAt).HasColumnName("withdrawn_at");
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leaves_employee");
        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leaves_type");
        builder.HasIndex(x => new { x.EmployeeId, x.StartDate, x.EndDate }).HasDatabaseName("ix_leaves_employee_dates");
        builder.HasIndex(x => new { x.Status, x.StartDate }).HasDatabaseName("ix_leaves_status_start");
    }
}
