using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230021_AdministrativeAffairs")]
public sealed class AdministrativeAffairs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE administration.administrative_tasks (
                id uuid NOT NULL CONSTRAINT pk_administrative_tasks PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                title varchar(200) NOT NULL,
                description varchar(2000) NULL,
                responsible_user_id uuid NOT NULL,
                due_date date NOT NULL,
                recurrence_unit varchar(20) NOT NULL,
                recurrence_interval integer NOT NULL DEFAULT 0,
                reminder_days_before integer NOT NULL DEFAULT 7,
                status varchar(30) NOT NULL,
                completion_count integer NOT NULL DEFAULT 0,
                last_completed_at timestamptz NULL,
                last_completed_by uuid NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_administrative_tasks_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_administrative_tasks_responsible_user FOREIGN KEY (responsible_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_administrative_tasks_last_completed_by FOREIGN KEY (last_completed_by) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_administrative_tasks_status CHECK (status IN ('OPEN','PAUSED','COMPLETED','CLOSED')),
                CONSTRAINT ck_administrative_tasks_recurrence CHECK (recurrence_unit IN ('NONE','DAILY','WEEKLY','MONTHLY','YEARLY')),
                CONSTRAINT ck_administrative_tasks_interval CHECK ((recurrence_unit = 'NONE' AND recurrence_interval = 0) OR (recurrence_unit <> 'NONE' AND recurrence_interval BETWEEN 1 AND 365)),
                CONSTRAINT ck_administrative_tasks_reminder CHECK (reminder_days_before BETWEEN 0 AND 365),
                CONSTRAINT ck_administrative_tasks_completion_count CHECK (completion_count >= 0)
            );
            CREATE UNIQUE INDEX ux_administrative_tasks_company_code ON administration.administrative_tasks(company_id, code) WHERE deleted_at IS NULL;
            CREATE INDEX ix_administrative_tasks_company_status_due ON administration.administrative_tasks(company_id, status, due_date);
            CREATE INDEX ix_administrative_tasks_responsible_due ON administration.administrative_tasks(responsible_user_id, status, due_date);

            CREATE TABLE administration.administrative_task_completions (
                id uuid NOT NULL CONSTRAINT pk_administrative_task_completions PRIMARY KEY,
                company_id uuid NOT NULL,
                task_id uuid NOT NULL,
                due_date_snapshot date NOT NULL,
                completed_local_date date NOT NULL,
                completed_at timestamptz NOT NULL,
                completed_by uuid NOT NULL,
                note varchar(1000) NULL,
                CONSTRAINT fk_administrative_task_completions_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_administrative_task_completions_task FOREIGN KEY (task_id) REFERENCES administration.administrative_tasks(id) ON DELETE RESTRICT,
                CONSTRAINT fk_administrative_task_completions_user FOREIGN KEY (completed_by) REFERENCES system.users(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_administrative_task_completions_task_time ON administration.administrative_task_completions(task_id, completed_at DESC);

            CREATE TABLE administration.administrative_contracts (
                id uuid NOT NULL CONSTRAINT pk_administrative_contracts PRIMARY KEY,
                company_id uuid NOT NULL,
                contract_no varchar(100) NOT NULL,
                title varchar(200) NOT NULL,
                counterparty varchar(200) NOT NULL,
                responsible_user_id uuid NOT NULL,
                start_date date NOT NULL,
                end_date date NOT NULL,
                reminder_days_before integer NOT NULL DEFAULT 30,
                auto_renewal boolean NOT NULL DEFAULT FALSE,
                contract_value numeric(18,2) NULL,
                currency char(3) NULL,
                status varchar(30) NOT NULL,
                note varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_administrative_contracts_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_administrative_contracts_responsible_user FOREIGN KEY (responsible_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_administrative_contracts_dates CHECK (end_date >= start_date),
                CONSTRAINT ck_administrative_contracts_reminder CHECK (reminder_days_before BETWEEN 0 AND 730),
                CONSTRAINT ck_administrative_contracts_value CHECK (contract_value IS NULL OR contract_value >= 0),
                CONSTRAINT ck_administrative_contracts_currency CHECK ((contract_value IS NULL AND currency IS NULL) OR (contract_value IS NOT NULL AND currency ~ '^[A-Z]{3}$')),
                CONSTRAINT ck_administrative_contracts_status CHECK (status IN ('ACTIVE','CLOSED'))
            );
            CREATE UNIQUE INDEX ux_administrative_contracts_company_no ON administration.administrative_contracts(company_id, contract_no) WHERE deleted_at IS NULL;
            CREATE INDEX ix_administrative_contracts_company_status_end ON administration.administrative_contracts(company_id, status, end_date);
            CREATE INDEX ix_administrative_contracts_responsible_end ON administration.administrative_contracts(responsible_user_id, end_date);

            CREATE TABLE administration.reminder_events (
                id uuid NOT NULL CONSTRAINT pk_reminder_events PRIMARY KEY,
                company_id uuid NOT NULL,
                event_type varchar(80) NOT NULL,
                source_type varchar(50) NOT NULL,
                source_id uuid NOT NULL,
                due_date date NULL,
                severity varchar(20) NOT NULL,
                dedupe_key varchar(300) NOT NULL,
                message varchar(1000) NOT NULL,
                metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_reminder_events_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_reminder_events_severity CHECK (severity IN ('INFO','NORMAL','IMPORTANT','CRITICAL'))
            );
            CREATE UNIQUE INDEX ux_reminder_events_dedupe ON administration.reminder_events(dedupe_key);
            CREATE INDEX ix_reminder_events_company_time ON administration.reminder_events(company_id, created_at DESC);
            CREATE INDEX ix_reminder_events_type_due ON administration.reminder_events(event_type, due_date);

            CREATE OR REPLACE FUNCTION administration.prevent_administrative_history_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'ADMIN_HISTORY_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_administrative_task_completions_immutable BEFORE UPDATE OR DELETE ON administration.administrative_task_completions FOR EACH ROW EXECUTE FUNCTION administration.prevent_administrative_history_mutation();
            CREATE TRIGGER trg_reminder_events_immutable BEFORE UPDATE OR DELETE ON administration.reminder_events FOR EACH ROW EXECUTE FUNCTION administration.prevent_administrative_history_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000097', 'administration.task.view', 'View Administrative Tasks', 'Administration', 'View recurring administrative task tracking.', TRUE),
                ('20000000-0000-0000-0000-000000000098', 'administration.task.manage', 'Manage Administrative Tasks', 'Administration', 'Create, complete, pause, resume and close administrative tasks.', TRUE),
                ('20000000-0000-0000-0000-000000000099', 'administration.contract.view', 'View Administrative Contracts', 'Administration', 'View contract dates, counterparties and effective expiry status.', TRUE),
                ('20000000-0000-0000-0000-000000000100', 'administration.contract.manage', 'Manage Administrative Contracts', 'Administration', 'Create and close administrative contracts.', TRUE),
                ('20000000-0000-0000-0000-000000000101', 'administration.reminder.view', 'View Administrative Reminders', 'Administration', 'View vehicle, task and contract reminder events.', TRUE),
                ('20000000-0000-0000-0000-000000000102', 'administration.reminder.process', 'Process Administrative Reminders', 'Administration', 'Manually trigger idempotent reminder event processing.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000097', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000097', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000098', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000098', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000099', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000099', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000100', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000100', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000101', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000102', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN ('30000000-0000-0000-0000-000000000097','30000000-0000-0000-0000-000000000098','30000000-0000-0000-0000-000000000099','30000000-0000-0000-0000-000000000100','30000000-0000-0000-0000-000000000101','30000000-0000-0000-0000-000000000102');
            DELETE FROM system.permissions WHERE id IN ('20000000-0000-0000-0000-000000000097','20000000-0000-0000-0000-000000000098','20000000-0000-0000-0000-000000000099','20000000-0000-0000-0000-000000000100','20000000-0000-0000-0000-000000000101','20000000-0000-0000-0000-000000000102');
            DROP TRIGGER IF EXISTS trg_reminder_events_immutable ON administration.reminder_events;
            DROP TRIGGER IF EXISTS trg_administrative_task_completions_immutable ON administration.administrative_task_completions;
            DROP FUNCTION IF EXISTS administration.prevent_administrative_history_mutation();
            DROP TABLE IF EXISTS administration.reminder_events;
            DROP TABLE IF EXISTS administration.administrative_contracts;
            DROP TABLE IF EXISTS administration.administrative_task_completions;
            DROP TABLE IF EXISTS administration.administrative_tasks;
            """);
    }
}
