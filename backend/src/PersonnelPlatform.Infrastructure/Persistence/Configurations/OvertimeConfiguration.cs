using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Attendance;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class OvertimeRequestConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> builder)
    {
        builder.ToTable("overtime_requests", DatabaseSchemas.Attendance);
        builder.HasKey(x => x.Id).HasName("pk_overtime_requests");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.DailyAttendanceId).HasColumnName("daily_attendance_id");
        builder.Property(x => x.SourceDailyVersion).HasColumnName("source_daily_version");
        builder.Property(x => x.AttendanceDate).HasColumnName("attendance_date");
        builder.Property(x => x.CandidateMinutes).HasColumnName("candidate_minutes");
        builder.Property(x => x.RequestedMinutes).HasColumnName("requested_minutes");
        builder.Property(x => x.ApprovedMinutes).HasColumnName("approved_minutes");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000);
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(x => x.ManagerDecidedAt).HasColumnName("manager_decided_at");
        builder.Property(x => x.ManagerDecidedBy).HasColumnName("manager_decided_by");
        builder.Property(x => x.HrDecidedAt).HasColumnName("hr_decided_at");
        builder.Property(x => x.HrDecidedBy).HasColumnName("hr_decided_by");
        builder.Property(x => x.RejectedAt).HasColumnName("rejected_at");
        builder.Property(x => x.RejectedBy).HasColumnName("rejected_by");
        builder.Property(x => x.DecisionNote).HasColumnName("decision_note").HasMaxLength(2000);
        AttendanceConfigurationHelpers.ConfigureAudit(builder);
        builder.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).HasDatabaseName("ix_overtime_employee_date");
        builder.HasIndex(x => new { x.CompanyId, x.Status, x.AttendanceDate }).HasDatabaseName("ix_overtime_company_status_date");
        builder.HasIndex(x => x.DailyAttendanceId).HasDatabaseName("ix_overtime_daily_model");
    }
}
