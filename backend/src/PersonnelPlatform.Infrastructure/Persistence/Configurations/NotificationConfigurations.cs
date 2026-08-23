using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Notification;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class NotificationConfigurationHelpers
{
    public static void Audit<T>(EntityTypeBuilder<T> b) where T : AuditableEntity
    {
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at"); b.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.ToTable("templates", DatabaseSchemas.Notification); b.HasKey(x => x.Id).HasName("pk_notification_templates");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(80); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x => x.TitleTemplate).HasColumnName("title_template").HasMaxLength(300); b.Property(x => x.BodyTemplate).HasColumnName("body_template").HasMaxLength(2000); b.Property(x => x.DeepLinkTemplate).HasColumnName("deep_link_template").HasMaxLength(1000); b.Property(x => x.IsActive).HasColumnName("is_active"); NotificationConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_templates_company");
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_notification_templates_company_code");
    }
}

public sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> b)
    {
        b.ToTable("rules", DatabaseSchemas.Notification); b.HasKey(x => x.Id).HasName("pk_notification_rules");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(80); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(50); b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); b.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20); b.Property(x => x.RecipientKind).HasColumnName("recipient_kind").HasMaxLength(30); b.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id"); b.Property(x => x.RecipientRoleId).HasColumnName("recipient_role_id"); b.Property(x => x.TemplateId).HasColumnName("template_id"); b.Property(x => x.EscalateAfterMinutes).HasColumnName("escalate_after_minutes"); b.Property(x => x.EscalationRecipientKind).HasColumnName("escalation_recipient_kind").HasMaxLength(30); b.Property(x => x.EscalationUserId).HasColumnName("escalation_user_id"); b.Property(x => x.EscalationRoleId).HasColumnName("escalation_role_id"); b.Property(x => x.IsActive).HasColumnName("is_active"); NotificationConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_company"); b.HasOne<NotificationTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_template"); b.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_recipient_user"); b.HasOne<Role>().WithMany().HasForeignKey(x => x.RecipientRoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_recipient_role"); b.HasOne<User>().WithMany().HasForeignKey(x => x.EscalationUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_escalation_user"); b.HasOne<Role>().WithMany().HasForeignKey(x => x.EscalationRoleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_rules_escalation_role");
        b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasFilter("deleted_at IS NULL").HasDatabaseName("ux_notification_rules_company_code"); b.HasIndex(x => new { x.CompanyId, x.SourceModule, x.EventType, x.IsActive }).HasDatabaseName("ix_notification_rules_source");
    }
}

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> b)
    {
        b.ToTable("notifications", DatabaseSchemas.Notification); b.HasKey(x => x.Id).HasName("pk_notifications");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.UserId).HasColumnName("user_id"); b.Property(x => x.RuleId).HasColumnName("rule_id"); b.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(50); b.Property(x => x.SourceEventType).HasColumnName("source_event_type").HasMaxLength(80); b.Property(x => x.SourceEventId).HasColumnName("source_event_id"); b.Property(x => x.SourceEntityId).HasColumnName("source_entity_id"); b.Property(x => x.ParentNotificationId).HasColumnName("parent_notification_id"); b.Property(x => x.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(400); b.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(20); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(300); b.Property(x => x.Body).HasColumnName("body").HasMaxLength(2000); b.Property(x => x.DeepLink).HasColumnName("deep_link").HasMaxLength(1000); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(30); b.Property(x => x.DueAt).HasColumnName("due_at"); b.Property(x => x.SnoozedUntil).HasColumnName("snoozed_until"); b.Property(x => x.SeenAt).HasColumnName("seen_at"); b.Property(x => x.StartedAt).HasColumnName("started_at"); b.Property(x => x.CompletedAt).HasColumnName("completed_at"); b.Property(x => x.EscalatedAt).HasColumnName("escalated_at"); b.Property(x => x.EscalationLevel).HasColumnName("escalation_level"); NotificationConfigurationHelpers.Audit(b);
        b.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notifications_company"); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notifications_user"); b.HasOne<NotificationRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notifications_rule"); b.HasOne<UserNotification>().WithMany().HasForeignKey(x => x.ParentNotificationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notifications_parent");
        b.HasIndex(x => x.DedupeKey).IsUnique().HasDatabaseName("ux_notifications_dedupe"); b.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt }).HasDatabaseName("ix_notifications_user_status_time"); b.HasIndex(x => new { x.CompanyId, x.Status }).HasDatabaseName("ix_notifications_company_status");
    }
}

public sealed class NotificationHistoryConfiguration : IEntityTypeConfiguration<NotificationHistory>
{
    public void Configure(EntityTypeBuilder<NotificationHistory> b)
    {
        b.ToTable("history", DatabaseSchemas.Notification); b.HasKey(x => x.Id).HasName("pk_notification_history");
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CompanyId).HasColumnName("company_id"); b.Property(x => x.NotificationId).HasColumnName("notification_id"); b.Property(x => x.UserId).HasColumnName("user_id"); b.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80); b.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30); b.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30); b.Property(x => x.ActorUserId).HasColumnName("actor_user_id"); b.Property(x => x.OccurredAt).HasColumnName("occurred_at"); b.Property(x => x.DetailsJson).HasColumnName("details_json").HasColumnType("jsonb");
        b.HasOne<UserNotification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_history_notification"); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_history_user"); b.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_notification_history_actor"); b.HasIndex(x => new { x.NotificationId, x.OccurredAt }).HasDatabaseName("ix_notification_history_notification_time");
    }
}
