using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class WorkCalendarConfiguration : IEntityTypeConfiguration<WorkCalendar>
{
    public void Configure(EntityTypeBuilder<WorkCalendar> builder)
    {
        builder.ToTable("work_calendars", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.IsDefault });
    }
}

public sealed class WorkCalendarDayConfiguration : IEntityTypeConfiguration<WorkCalendarDay>
{
    public void Configure(EntityTypeBuilder<WorkCalendarDay> builder)
    {
        builder.ToTable("work_calendar_days", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DayType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.WorkCalendarId, x.Date }).IsUnique();
    }
}

public sealed class ShiftDefinitionConfiguration : IEntityTypeConfiguration<ShiftDefinition>
{
    public void Configure(EntityTypeBuilder<ShiftDefinition> builder)
    {
        builder.ToTable("shifts", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartTime).HasColumnType("time without time zone");
        builder.Property(x => x.EndTime).HasColumnType("time without time zone");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
    }
}

public sealed class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.ToTable("employee_shift_assignments", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmployeeId, x.ValidFrom, x.ValidUntil });
        builder.HasIndex(x => x.ShiftId);
        builder.HasIndex(x => x.WorkCalendarId);
    }
}
