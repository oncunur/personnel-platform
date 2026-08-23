using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Audit;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("logs", DatabaseSchemas.Audit);
        builder.HasKey(x => x.Id).HasName("pk_audit_logs");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Succeeded).HasColumnName("succeeded").IsRequired();
        builder.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.ActorUsername).HasColumnName("actor_username").HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(100);
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(100);
        builder.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(100);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(500);
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");

        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("ix_audit_logs_occurred_at");
        builder.HasIndex(x => new { x.Category, x.EventType, x.OccurredAt }).HasDatabaseName("ix_audit_logs_category_event_time");
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt }).HasDatabaseName("ix_audit_logs_actor_time");
    }
}
