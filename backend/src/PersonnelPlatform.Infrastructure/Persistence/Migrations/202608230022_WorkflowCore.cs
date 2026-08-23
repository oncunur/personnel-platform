using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230022_WorkflowCore")]
public sealed class WorkflowCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS workflow;

            CREATE TABLE workflow.request_types (
                id uuid NOT NULL CONSTRAINT pk_workflow_request_types PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(200) NOT NULL,
                description varchar(2000) NULL,
                sla_minutes integer NOT NULL,
                required_fields_json jsonb NOT NULL DEFAULT '[]'::jsonb,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_workflow_request_types_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_request_types_sla CHECK (sla_minutes BETWEEN 1 AND 525600),
                CONSTRAINT ck_workflow_request_types_required_fields CHECK (jsonb_typeof(required_fields_json) = 'array')
            );
            CREATE UNIQUE INDEX ux_workflow_request_types_company_code ON workflow.request_types(company_id, code) WHERE deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_workflow_request_types_company_id ON workflow.request_types(company_id, id);

            CREATE TABLE workflow.approval_step_definitions (
                id uuid NOT NULL CONSTRAINT pk_workflow_step_definitions PRIMARY KEY,
                company_id uuid NOT NULL,
                request_type_id uuid NOT NULL,
                step_order integer NOT NULL,
                name varchar(150) NOT NULL,
                target_kind varchar(20) NOT NULL,
                approver_user_id uuid NULL,
                approver_role_id uuid NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_workflow_steps_request_type FOREIGN KEY (request_type_id) REFERENCES workflow.request_types(id) ON DELETE CASCADE,
                CONSTRAINT fk_workflow_steps_company_type FOREIGN KEY (company_id, request_type_id) REFERENCES workflow.request_types(company_id, id) ON DELETE CASCADE,
                CONSTRAINT fk_workflow_steps_user FOREIGN KEY (approver_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_steps_role FOREIGN KEY (approver_role_id) REFERENCES system.roles(id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_steps_order CHECK (step_order BETWEEN 1 AND 100),
                CONSTRAINT ck_workflow_steps_kind CHECK (target_kind IN ('USER','ROLE')),
                CONSTRAINT ck_workflow_steps_target CHECK (
                    (target_kind = 'USER' AND approver_user_id IS NOT NULL AND approver_role_id IS NULL)
                    OR (target_kind = 'ROLE' AND approver_role_id IS NOT NULL AND approver_user_id IS NULL)
                )
            );
            CREATE UNIQUE INDEX ux_workflow_steps_type_order ON workflow.approval_step_definitions(request_type_id, step_order) WHERE deleted_at IS NULL;
            CREATE INDEX ix_workflow_steps_user ON workflow.approval_step_definitions(approver_user_id) WHERE approver_user_id IS NOT NULL;
            CREATE INDEX ix_workflow_steps_role ON workflow.approval_step_definitions(approver_role_id) WHERE approver_role_id IS NOT NULL;

            CREATE TABLE workflow.request_number_counters (
                company_id uuid NOT NULL,
                year integer NOT NULL,
                next_value integer NOT NULL,
                CONSTRAINT pk_workflow_request_number_counters PRIMARY KEY (company_id, year),
                CONSTRAINT fk_workflow_request_counter_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_request_counter_year CHECK (year BETWEEN 2000 AND 2200),
                CONSTRAINT ck_workflow_request_counter_value CHECK (next_value >= 1)
            );

            CREATE TABLE workflow.requests (
                id uuid NOT NULL CONSTRAINT pk_workflow_requests PRIMARY KEY,
                company_id uuid NOT NULL,
                request_no varchar(40) NOT NULL,
                request_type_id uuid NOT NULL,
                requester_user_id uuid NOT NULL,
                employee_id uuid NULL,
                priority varchar(20) NOT NULL,
                request_data_json jsonb NOT NULL,
                status varchar(30) NOT NULL,
                current_step_order integer NOT NULL DEFAULT 0,
                sla_minutes_snapshot integer NOT NULL DEFAULT 0,
                submitted_at timestamptz NULL,
                due_at timestamptz NULL,
                resolved_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_workflow_requests_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_requests_type FOREIGN KEY (request_type_id) REFERENCES workflow.request_types(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_requests_company_type FOREIGN KEY (company_id, request_type_id) REFERENCES workflow.request_types(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_requests_requester FOREIGN KEY (requester_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_requests_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_requests_company_employee FOREIGN KEY (company_id, employee_id) REFERENCES hr.employees(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_requests_priority CHECK (priority IN ('INFO','NORMAL','IMPORTANT','CRITICAL')),
                CONSTRAINT ck_workflow_requests_status CHECK (status IN ('DRAFT','IN_APPROVAL','APPROVED','REJECTED','CANCELLED')),
                CONSTRAINT ck_workflow_requests_payload CHECK (jsonb_typeof(request_data_json) = 'object'),
                CONSTRAINT ck_workflow_requests_step CHECK (current_step_order >= 0),
                CONSTRAINT ck_workflow_requests_sla CHECK (sla_minutes_snapshot BETWEEN 0 AND 525600),
                CONSTRAINT ck_workflow_requests_submit_fields CHECK ((status = 'DRAFT' AND submitted_at IS NULL AND due_at IS NULL) OR status <> 'DRAFT'),
                CONSTRAINT ck_workflow_requests_resolution CHECK ((status IN ('APPROVED','REJECTED','CANCELLED') AND resolved_at IS NOT NULL) OR status NOT IN ('APPROVED','REJECTED','CANCELLED'))
            );
            CREATE UNIQUE INDEX ux_workflow_requests_company_no ON workflow.requests(company_id, request_no);
            CREATE UNIQUE INDEX ux_workflow_requests_company_id ON workflow.requests(company_id, id);
            CREATE INDEX ix_workflow_requests_company_status_time ON workflow.requests(company_id, status, created_at DESC);
            CREATE INDEX ix_workflow_requests_employee_time ON workflow.requests(employee_id, created_at DESC) WHERE employee_id IS NOT NULL;
            CREATE INDEX ix_workflow_requests_requester_time ON workflow.requests(requester_user_id, created_at DESC);
            CREATE INDEX ix_workflow_requests_due ON workflow.requests(due_at) WHERE status = 'IN_APPROVAL';

            CREATE TABLE workflow.request_approvals (
                id uuid NOT NULL CONSTRAINT pk_workflow_request_approvals PRIMARY KEY,
                company_id uuid NOT NULL,
                request_id uuid NOT NULL,
                step_order integer NOT NULL,
                step_name_snapshot varchar(150) NOT NULL,
                target_kind_snapshot varchar(20) NOT NULL,
                approver_user_id_snapshot uuid NULL,
                approver_role_id_snapshot uuid NULL,
                status varchar(20) NOT NULL,
                action_by_user_id uuid NULL,
                action_at timestamptz NULL,
                comment varchar(1000) NULL,
                CONSTRAINT fk_workflow_approvals_request FOREIGN KEY (request_id) REFERENCES workflow.requests(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_approvals_company_request FOREIGN KEY (company_id, request_id) REFERENCES workflow.requests(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_approvals_target_user FOREIGN KEY (approver_user_id_snapshot) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_approvals_target_role FOREIGN KEY (approver_role_id_snapshot) REFERENCES system.roles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_approvals_action_user FOREIGN KEY (action_by_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_approvals_order CHECK (step_order BETWEEN 1 AND 100),
                CONSTRAINT ck_workflow_approvals_kind CHECK (target_kind_snapshot IN ('USER','ROLE')),
                CONSTRAINT ck_workflow_approvals_target CHECK (
                    (target_kind_snapshot = 'USER' AND approver_user_id_snapshot IS NOT NULL AND approver_role_id_snapshot IS NULL)
                    OR (target_kind_snapshot = 'ROLE' AND approver_role_id_snapshot IS NOT NULL AND approver_user_id_snapshot IS NULL)
                ),
                CONSTRAINT ck_workflow_approvals_status CHECK (status IN ('WAITING','PENDING','APPROVED','REJECTED')),
                CONSTRAINT ck_workflow_approvals_action CHECK ((status IN ('APPROVED','REJECTED') AND action_by_user_id IS NOT NULL AND action_at IS NOT NULL) OR status IN ('WAITING','PENDING'))
            );
            CREATE UNIQUE INDEX ux_workflow_approvals_request_step ON workflow.request_approvals(request_id, step_order);
            CREATE INDEX ix_workflow_approvals_user_pending ON workflow.request_approvals(approver_user_id_snapshot) WHERE status = 'PENDING' AND approver_user_id_snapshot IS NOT NULL;
            CREATE INDEX ix_workflow_approvals_role_pending ON workflow.request_approvals(approver_role_id_snapshot) WHERE status = 'PENDING' AND approver_role_id_snapshot IS NOT NULL;

            CREATE TABLE workflow.request_history (
                id uuid NOT NULL CONSTRAINT pk_workflow_request_history PRIMARY KEY,
                company_id uuid NOT NULL,
                request_id uuid NOT NULL,
                event_type varchar(80) NOT NULL,
                from_status varchar(30) NULL,
                to_status varchar(30) NOT NULL,
                actor_user_id uuid NOT NULL,
                occurred_at timestamptz NOT NULL,
                details_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                CONSTRAINT fk_workflow_history_request FOREIGN KEY (request_id) REFERENCES workflow.requests(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_history_company_request FOREIGN KEY (company_id, request_id) REFERENCES workflow.requests(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_history_actor FOREIGN KEY (actor_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_history_details CHECK (jsonb_typeof(details_json) = 'object')
            );
            CREATE INDEX ix_workflow_history_request_time ON workflow.request_history(request_id, occurred_at DESC);

            CREATE TABLE workflow.sla_events (
                id uuid NOT NULL CONSTRAINT pk_workflow_sla_events PRIMARY KEY,
                company_id uuid NOT NULL,
                request_id uuid NOT NULL,
                event_type varchar(80) NOT NULL,
                severity varchar(20) NOT NULL,
                dedupe_key varchar(300) NOT NULL,
                message varchar(1000) NOT NULL,
                metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_workflow_sla_request FOREIGN KEY (request_id) REFERENCES workflow.requests(id) ON DELETE RESTRICT,
                CONSTRAINT fk_workflow_sla_company_request FOREIGN KEY (company_id, request_id) REFERENCES workflow.requests(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_workflow_sla_severity CHECK (severity IN ('INFO','NORMAL','IMPORTANT','CRITICAL')),
                CONSTRAINT ck_workflow_sla_metadata CHECK (jsonb_typeof(metadata_json) = 'object')
            );
            CREATE UNIQUE INDEX ux_workflow_sla_dedupe ON workflow.sla_events(dedupe_key);
            CREATE INDEX ix_workflow_sla_company_time ON workflow.sla_events(company_id, created_at DESC);
            CREATE INDEX ix_workflow_sla_request_time ON workflow.sla_events(request_id, created_at DESC);

            CREATE OR REPLACE FUNCTION workflow.prevent_append_only_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'WORKFLOW_HISTORY_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_workflow_history_immutable BEFORE UPDATE OR DELETE ON workflow.request_history FOR EACH ROW EXECUTE FUNCTION workflow.prevent_append_only_mutation();
            CREATE TRIGGER trg_workflow_sla_immutable BEFORE UPDATE OR DELETE ON workflow.sla_events FOR EACH ROW EXECUTE FUNCTION workflow.prevent_append_only_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000103', 'workflow.request_type.view', 'View Request Types', 'Workflow', 'View request type and approval workflow definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000104', 'workflow.request_type.manage', 'Manage Request Types', 'Workflow', 'Create and configure request types, SLA and approval steps.', TRUE),
                ('20000000-0000-0000-0000-000000000105', 'workflow.request.view', 'View Requests', 'Workflow', 'View requests and request timelines within scope.', TRUE),
                ('20000000-0000-0000-0000-000000000106', 'workflow.request.create', 'Create Requests', 'Workflow', 'Create and submit workflow requests.', TRUE),
                ('20000000-0000-0000-0000-000000000107', 'workflow.request.manage', 'Manage Requests', 'Workflow', 'Manage requests within authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000108', 'workflow.request.approve', 'Approve Requests', 'Workflow', 'Approve or reject assigned workflow steps.', TRUE),
                ('20000000-0000-0000-0000-000000000109', 'workflow.sla.view', 'View Workflow SLA', 'Workflow', 'View workflow SLA warning and escalation events.', TRUE),
                ('20000000-0000-0000-0000-000000000110', 'workflow.sla.process', 'Process Workflow SLA', 'Workflow', 'Run scoped SLA threshold processing manually.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000103', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000103', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000104', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000104', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000105', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000105', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000106', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000106', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000107', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000107', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000108', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000108', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000109', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000109', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000110', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000110', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN ('30000000-0000-0000-0000-000000000103','30000000-0000-0000-0000-000000000104','30000000-0000-0000-0000-000000000105','30000000-0000-0000-0000-000000000106','30000000-0000-0000-0000-000000000107','30000000-0000-0000-0000-000000000108','30000000-0000-0000-0000-000000000109','30000000-0000-0000-0000-000000000110');
            DELETE FROM system.permissions WHERE id IN ('20000000-0000-0000-0000-000000000103','20000000-0000-0000-0000-000000000104','20000000-0000-0000-0000-000000000105','20000000-0000-0000-0000-000000000106','20000000-0000-0000-0000-000000000107','20000000-0000-0000-0000-000000000108','20000000-0000-0000-0000-000000000109','20000000-0000-0000-0000-000000000110');
            DROP TRIGGER IF EXISTS trg_workflow_sla_immutable ON workflow.sla_events;
            DROP TRIGGER IF EXISTS trg_workflow_history_immutable ON workflow.request_history;
            DROP FUNCTION IF EXISTS workflow.prevent_append_only_mutation();
            DROP TABLE IF EXISTS workflow.sla_events;
            DROP TABLE IF EXISTS workflow.request_history;
            DROP TABLE IF EXISTS workflow.request_approvals;
            DROP TABLE IF EXISTS workflow.requests;
            DROP TABLE IF EXISTS workflow.request_number_counters;
            DROP TABLE IF EXISTS workflow.approval_step_definitions;
            DROP TABLE IF EXISTS workflow.request_types;
            DROP SCHEMA IF EXISTS workflow;
            """);
    }
}
