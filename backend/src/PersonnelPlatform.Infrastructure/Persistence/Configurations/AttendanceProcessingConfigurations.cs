using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class RawAttendanceEventConfiguration : IEntityTypeConfiguration<RawAttendanceEvent>
{
    public void Configure(EntityTypeBuilder<RawAttendanceEvent> builder)
    {
        builder.ToTable("raw_events", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.EventAt).IsRequired();
        builder.Property(x => x.LocalDate).IsRequired();
        builder.Property(x => x.LocalTime).HasColumnType("time without time zone").IsRequired();
        builder.Property(x => x.DeviceCode).HasMaxLength(100);
        builder.Property(x => x.ExternalEventId).HasMaxLength(200);
        builder.Property(x => x.RawPayloadJson).HasColumnType("text");
        builder.HasIndex(x => new { x.EmployeeId, x.LocalDate, x.LocalTime });
        builder.HasIndex(x => new { x.CompanyId, x.Source, x.ExternalEventId });
    }
}

public sealed class DailyAttendanceConfiguration : IEntityTypeConfiguration<DailyAttendance>
{
    public void Configure(EntityTypeBuilder<DailyAttendance> builder)
    {
        builder.ToTable("daily_attendance", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProcessingStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CalculationMessage).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.AttendanceDate, x.ProcessingStatus });
        builder.HasIndex(x => x.ShiftAssignmentId);
        builder.HasIndex(x => x.LeaveId);
    }
}
