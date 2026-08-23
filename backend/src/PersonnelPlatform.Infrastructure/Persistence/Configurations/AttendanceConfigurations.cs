using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCalendar> builder)
    {
        builder.ToTable("work_calendars", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_work_calendars");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.IsDefault).HasColumnName("is_default");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_work_calendars_company_code");
        builder.HasIndex(x => new { x.CompanyId, x.IsDefault }).HasDatabaseName("ix_work_calendars_company_default_model");
    }
}

public sealed class WorkCalendarDayConfiguration : IEntityTypeConfiguration<WorkCalendarDay>
{
    public void Configure(EntityTypeBuilder<WorkCalendarDay> builder)
    {
        builder.ToTable("work_calendar_days", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_work_calendar_days");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkCalendarId).HasColumnName("work_calendar_id");
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.DayType).HasColumnName("day_type").HasMaxLength(30).IsRequired();
        builder.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes");
        builder.Property(x => x.IsPaid).HasColumnName("is_paid");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.WorkCalendarId, x.Date }).IsUnique().HasDatabaseName("ux_work_calendar_days_calendar_date");
    }
}

public sealed class ShiftDefinitionConfiguration : IEntityTypeConfiguration<ShiftDefinition>
{
    public void Configure(EntityTypeBuilder<ShiftDefinition> builder)
    {
        builder.ToTable("shifts", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_shifts");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartTime).HasColumnName("start_time").HasColumnType("time without time zone");
        builder.Property(x => x.EndTime).HasColumnName("end_time").HasColumnType("time without time zone");
        builder.Property(x => x.BreakMinutes).HasColumnName("break_minutes");
        builder.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes");
        builder.Property(x => x.GraceInMinutes).HasColumnName("grace_in_minutes");
        builder.Property(x => x.GraceOutMinutes).HasColumnName("grace_out_minutes");
        builder.Property(x => x.CrossesMidnight).HasColumnName("crosses_midnight");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_shifts_company_code");
    }
}

public sealed class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.ToTable("employee_shift_assignments", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_employee_shift_assignments");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ShiftId).HasColumnName("shift_id");
        builder.Property(x => x.WorkCalendarId).HasColumnName("work_calendar_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.EmployeeId, x.ValidFrom, x.ValidUntil }).HasDatabaseName("ix_employee_shift_assignments_employee_dates");
        builder.HasIndex(x => x.ShiftId).HasDatabaseName("ix_employee_shift_assignments_shift");
        builder.HasIndex(x => x.WorkCalendarId).HasDatabaseName("ix_employee_shift_assignments_calendar");
    }
}
