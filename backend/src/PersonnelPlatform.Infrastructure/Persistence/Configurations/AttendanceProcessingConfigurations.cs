using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class RawAttendanceEventConfiguration : IEntityTypeConfiguration<RawAttendanceEvent>
{
    public void Configure(EntityTypeBuilder<RawAttendanceEvent> builder)
    {
        builder.ToTable("raw_events", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_raw_events");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(20).IsRequired();
        builder.Property(x => x.EventAt).HasColumnName("event_at").IsRequired();
        builder.Property(x => x.LocalDate).HasColumnName("local_date").IsRequired();
        builder.Property(x => x.LocalTime).HasColumnName("local_time").HasColumnType("time without time zone").IsRequired();
        builder.Property(x => x.UtcOffsetMinutes).HasColumnName("utc_offset_minutes");
        builder.Property(x => x.DeviceCode).HasColumnName("device_code").HasMaxLength(100);
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200);
        builder.Property(x => x.RawPayloadJson).HasColumnName("raw_payload_json").HasColumnType("text");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.ReceivedBy).HasColumnName("received_by");
        builder.HasIndex(x => new { x.EmployeeId, x.LocalDate, x.LocalTime }).HasDatabaseName("ix_raw_events_employee_local");
        builder.HasIndex(x => new { x.CompanyId, x.Source, x.ExternalEventId }).HasDatabaseName("ix_raw_events_source_external_model");
    }
}

public sealed class DailyAttendanceConfiguration : IEntityTypeConfiguration<DailyAttendance>
{
    public void Configure(EntityTypeBuilder<DailyAttendance> builder)
    {
        builder.ToTable("daily_attendance", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_daily_attendance");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.AttendanceDate).HasColumnName("attendance_date");
        builder.Property(x => x.ShiftAssignmentId).HasColumnName("shift_assignment_id");
        builder.Property(x => x.ShiftId).HasColumnName("shift_id");
        builder.Property(x => x.WorkCalendarId).HasColumnName("work_calendar_id");
        builder.Property(x => x.LeaveId).HasColumnName("leave_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.PlannedMinutes).HasColumnName("planned_minutes");
        builder.Property(x => x.LeaveMinutes).HasColumnName("leave_minutes");
        builder.Property(x => x.WorkedMinutes).HasColumnName("worked_minutes");
        builder.Property(x => x.LateMinutes).HasColumnName("late_minutes");
        builder.Property(x => x.EarlyLeaveMinutes).HasColumnName("early_leave_minutes");
        builder.Property(x => x.OvertimeCandidateMinutes).HasColumnName("overtime_candidate_minutes");
        builder.Property(x => x.FirstInAt).HasColumnName("first_in_at");
        builder.Property(x => x.LastOutAt).HasColumnName("last_out_at");
        builder.Property(x => x.SourceSnapshotJson).HasColumnName("source_snapshot_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CalculationMessage).HasColumnName("calculation_message").HasMaxLength(2000);
        builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).IsUnique().HasDatabaseName("ux_daily_attendance_employee_date");
        builder.HasIndex(x => new { x.CompanyId, x.AttendanceDate, x.ProcessingStatus }).HasDatabaseName("ix_daily_attendance_company_date_status");
        builder.HasIndex(x => x.ShiftAssignmentId).HasDatabaseName("ix_daily_attendance_assignment");
        builder.HasIndex(x => x.LeaveId).HasDatabaseName("ix_daily_attendance_leave");
    }
}
