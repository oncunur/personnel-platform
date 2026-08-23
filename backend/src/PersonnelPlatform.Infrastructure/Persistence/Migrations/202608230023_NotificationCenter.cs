using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230023_NotificationCenter")]
public sealed class NotificationCenter : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS notification;

            CREATE TABLE notification.templates (
                id uuid NOT NULL CONSTRAINT pk_notification_templates PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(200) NOT NULL,
                title_template varchar(300) NOT NULL,
                body_template varchar(2000) NOT NULL,
                deep_link_template varchar(1000) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_notification_templates_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_notification_templates_company_code ON notification.templates(company_id, code) WHERE deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_notification_templates_company_id ON notification.templates(company_id, id);

            CREATE TABLE notification.rules (
                id uuid NOT NULL CONSTRAINT pk_notification_rules PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(200) NOT NULL,
                source_module varchar(50) NOT NULL,
                event_type varchar(80) NOT NULL,
                priority varchar(20) NOT NULL,
                recipient_kind varchar(30) NOT NULL,
                recipient_user_id uuid NULL,
                recipient_role_id uuid NULL,
                template_id uuid NOT NULL,
                escalate_after_minutes integer NULL,
                escalation_recipient_kind varchar(30) NULL,
                escalation_user_id uuid NULL,
                escalation_role_id uuid NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_notification_rules_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_template FOREIGN KEY (template_id) REFERENCES notification.templates(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_company_template FOREIGN KEY (company_id, template_id) REFERENCES notification.templates(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_recipient_user FOREIGN KEY (recipient_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_recipient_role FOREIGN KEY (recipient_role_id) REFERENCES system.roles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_escalation_user FOREIGN KEY (escalation_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_rules_escalation_role FOREIGN KEY (escalation_role_id) REFERENCES system.roles(id) ON DELETE RESTRICT,
                CONSTRAINT ck_notification_rules_priority CHECK (priority IN ('INFO','NORMAL','IMPORTANT','CRITICAL')),
                CONSTRAINT ck_notification_rules_recipient_kind CHECK (recipient_kind IN ('USER','ROLE','CURRENT_APPROVER','REQUESTER','RESPONSIBLE')),
                CONSTRAINT ck_notification_rules_recipient_target CHECK (
                    (recipient_kind = 'USER' AND recipient_user_id IS NOT NULL AND recipient_role_id IS NULL)
                    OR (recipient_kind = 'ROLE' AND recipient_role_id IS NOT NULL AND recipient_user_id IS NULL)
                    OR (recipient_kind IN ('CURRENT_APPROVER','REQUESTER','RESPONSIBLE') AND recipient_user_id IS NULL AND recipient_role_id IS NULL)
                ),
                CONSTRAINT ck_notification_rules_escalation CHECK (
                    (escalate_after_minutes IS NULL AND escalation_recipient_kind IS NULL AND escalation_user_id IS NULL AND escalation_role_id IS NULL)
                    OR (escalate_after_minutes BETWEEN 1 AND 525600 AND (
                        (escalation_recipient_kind = 'USER' AND escalation_user_id IS NOT NULL AND escalation_role_id IS NULL)
                        OR (escalation_recipient_kind = 'ROLE' AND escalation_role_id IS NOT NULL AND escalation_user_id IS NULL)
                        OR (escalation_recipient_kind = 'MANAGER' AND escalation_user_id IS NULL AND escalation_role_id IS NULL)
                    ))
                )
            );
            CREATE UNIQUE INDEX ux_notification_rules_company_code ON notification.rules(company_id, code) WHERE deleted_at IS NULL;
            CREATE INDEX ix_notification_rules_source ON notification.rules(company_id, source_module, event_type, is_active);

            CREATE TABLE notification.notifications (
                id uuid NOT NULL CONSTRAINT pk_notifications PRIMARY KEY,
                company_id uuid NOT NULL,
                user_id uuid NOT NULL,
                rule_id uuid NOT NULL,
                source_module varchar(50) NOT NULL,
                source_event_type varchar(80) NOT NULL,
                source_event_id uuid NOT NULL,
                source_entity_id uuid NULL,
                parent_notification_id uuid NULL,
                dedupe_key varchar(400) NOT NULL,
                priority varchar(20) NOT NULL,
                title varchar(300) NOT NULL,
                body varchar(2000) NOT NULL,
                deep_link varchar(1000) NOT NULL,
                status varchar(30) NOT NULL,
                due_at timestamptz NULL,
                snoozed_until timestamptz NULL,
                seen_at timestamptz NULL,
                started_at timestamptz NULL,
                completed_at timestamptz NULL,
                escalated_at timestamptz NULL,
                escalation_level integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_notifications_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notifications_user FOREIGN KEY (user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notifications_rule FOREIGN KEY (rule_id) REFERENCES notification.rules(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notifications_parent FOREIGN KEY (parent_notification_id) REFERENCES notification.notifications(id) ON DELETE RESTRICT,
                CONSTRAINT ck_notifications_priority CHECK (priority IN ('INFO','NORMAL','IMPORTANT','CRITICAL')),
                CONSTRAINT ck_notifications_status CHECK (status IN ('NEW','SEEN','IN_PROGRESS','COMPLETED','SNOOZED','ESCALATED')),
                CONSTRAINT ck_notifications_escalation_level CHECK (escalation_level BETWEEN 0 AND 100),
                CONSTRAINT ck_notifications_status_fields CHECK (
                    (status <> 'SNOOZED' OR snoozed_until IS NOT NULL)
                    AND (status <> 'COMPLETED' OR completed_at IS NOT NULL)
                    AND (status <> 'ESCALATED' OR escalated_at IS NOT NULL)
                )
            );
            CREATE UNIQUE INDEX ux_notifications_dedupe ON notification.notifications(dedupe_key);
            CREATE INDEX ix_notifications_user_status_time ON notification.notifications(user_id, status, created_at DESC);
            CREATE INDEX ix_notifications_company_status ON notification.notifications(company_id, status);
            CREATE INDEX ix_notifications_due_active ON notification.notifications(due_at) WHERE status IN ('NEW','SEEN','IN_PROGRESS','SNOOZED');
            CREATE INDEX ix_notifications_parent ON notification.notifications(parent_notification_id) WHERE parent_notification_id IS NOT NULL;

            CREATE TABLE notification.history (
                id uuid NOT NULL CONSTRAINT pk_notification_history PRIMARY KEY,
                company_id uuid NOT NULL,
                notification_id uuid NOT NULL,
                user_id uuid NOT NULL,
                event_type varchar(80) NOT NULL,
                from_status varchar(30) NULL,
                to_status varchar(30) NOT NULL,
                actor_user_id uuid NULL,
                occurred_at timestamptz NOT NULL,
                details_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                CONSTRAINT fk_notification_history_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_history_notification FOREIGN KEY (notification_id) REFERENCES notification.notifications(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_history_user FOREIGN KEY (user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_notification_history_actor FOREIGN KEY (actor_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_notification_history_details CHECK (jsonb_typeof(details_json) = 'object')
            );
            CREATE INDEX ix_notification_history_notification_time ON notification.history(notification_id, occurred_at DESC);

            CREATE OR REPLACE FUNCTION notification.prevent_history_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'NOTIFICATION_HISTORY_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_notification_history_immutable BEFORE UPDATE OR DELETE ON notification.history FOR EACH ROW EXECUTE FUNCTION notification.prevent_history_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000111', 'notification.rule.view', 'View Notification Rules', 'Notification', 'View notification templates and routing rules.', TRUE),
                ('20000000-0000-0000-0000-000000000112', 'notification.rule.manage', 'Manage Notification Rules', 'Notification', 'Create and update templates, recipients, priorities and escalation rules.', TRUE),
                ('20000000-0000-0000-0000-000000000113', 'notification.view', 'View Notifications', 'Notification', 'View own notifications and action center.', TRUE),
                ('20000000-0000-0000-0000-000000000114', 'notification.action', 'Act on Notifications', 'Notification', 'Mark, start, snooze and complete own notifications.', TRUE),
                ('20000000-0000-0000-0000-000000000115', 'notification.process', 'Process Notifications', 'Notification', 'Run scoped notification ingestion and escalation processing.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000111', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000111', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000112', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000112', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000113', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000113', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000114', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000114', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000115', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000115', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN ('30000000-0000-0000-0000-000000000111','30000000-0000-0000-0000-000000000112','30000000-0000-0000-0000-000000000113','30000000-0000-0000-0000-000000000114','30000000-0000-0000-0000-000000000115');
            DELETE FROM system.permissions WHERE id IN ('20000000-0000-0000-0000-000000000111','20000000-0000-0000-0000-000000000112','20000000-0000-0000-0000-000000000113','20000000-0000-0000-0000-000000000114','20000000-0000-0000-0000-000000000115');
            DROP TRIGGER IF EXISTS trg_notification_history_immutable ON notification.history;
            DROP FUNCTION IF EXISTS notification.prevent_history_mutation();
            DROP TABLE IF EXISTS notification.history;
            DROP TABLE IF EXISTS notification.notifications;
            DROP TABLE IF EXISTS notification.rules;
            DROP TABLE IF EXISTS notification.templates;
            DROP SCHEMA IF EXISTS notification;
            """);
    }
}
