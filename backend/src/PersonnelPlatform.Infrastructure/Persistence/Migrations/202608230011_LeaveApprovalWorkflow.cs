using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230011_LeaveApprovalWorkflow")]
public sealed class LeaveApprovalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS pgcrypto;

            CREATE TABLE system.user_employee_links (
                id uuid NOT NULL CONSTRAINT pk_user_employee_links PRIMARY KEY,
                user_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_user_employee_links_user FOREIGN KEY (user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_user_employee_links_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_user_employee_links_user ON system.user_employee_links(user_id) WHERE deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_user_employee_links_employee_active ON system.user_employee_links(employee_id) WHERE is_active = TRUE AND deleted_at IS NULL;

            CREATE TABLE hr.leave_approvals (
                id uuid NOT NULL CONSTRAINT pk_leave_approvals PRIMARY KEY,
                leave_id uuid NOT NULL,
                step_order integer NOT NULL,
                step_code varchar(30) NOT NULL,
                approver_employee_id uuid NULL,
                assigned_user_id uuid NULL,
                status varchar(30) NOT NULL,
                decided_by_user_id uuid NULL,
                decided_at timestamptz NULL,
                decision_note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_leave_approvals_leave FOREIGN KEY (leave_id) REFERENCES hr.leaves(id) ON DELETE CASCADE,
                CONSTRAINT fk_leave_approvals_approver_employee FOREIGN KEY (approver_employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_leave_approvals_assigned_user FOREIGN KEY (assigned_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT fk_leave_approvals_decided_user FOREIGN KEY (decided_by_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_leave_approvals_step CHECK (step_code IN ('MANAGER','HR')),
                CONSTRAINT ck_leave_approvals_status CHECK (status IN ('WAITING','PENDING','APPROVED','REJECTED','SKIPPED')),
                CONSTRAINT ck_leave_approvals_order CHECK (step_order > 0),
                CONSTRAINT ck_leave_approvals_manager CHECK (step_code <> 'MANAGER' OR status = 'SKIPPED' OR approver_employee_id IS NOT NULL)
            );
            CREATE UNIQUE INDEX ux_leave_approvals_leave_step ON hr.leave_approvals(leave_id, step_order) WHERE deleted_at IS NULL;
            CREATE INDEX ix_leave_approvals_status_step ON hr.leave_approvals(status, step_code);
            CREATE INDEX ix_leave_approvals_approver_employee ON hr.leave_approvals(approver_employee_id, status);

            CREATE TABLE hr.leave_approval_history (
                id uuid NOT NULL CONSTRAINT pk_leave_approval_history PRIMARY KEY,
                leave_id uuid NOT NULL,
                approval_id uuid NULL,
                action varchar(50) NOT NULL,
                step_code varchar(30) NULL,
                from_status varchar(30) NULL,
                to_status varchar(30) NULL,
                actor_user_id uuid NULL,
                occurred_at timestamptz NOT NULL,
                note varchar(1000) NULL,
                CONSTRAINT fk_leave_approval_history_leave FOREIGN KEY (leave_id) REFERENCES hr.leaves(id) ON DELETE CASCADE,
                CONSTRAINT fk_leave_approval_history_approval FOREIGN KEY (approval_id) REFERENCES hr.leave_approvals(id) ON DELETE SET NULL,
                CONSTRAINT fk_leave_approval_history_actor FOREIGN KEY (actor_user_id) REFERENCES system.users(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_leave_approval_history_leave_occurred ON hr.leave_approval_history(leave_id, occurred_at DESC);

            CREATE OR REPLACE FUNCTION hr.start_leave_approval_workflow()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                v_manager_employee_id uuid;
                v_manager_user_id uuid;
                v_manager_approval_id uuid;
                v_hr_approval_id uuid;
            BEGIN
                IF NEW.status = 'SUBMITTED' AND OLD.status = 'DRAFT' THEN
                    IF EXISTS (SELECT 1 FROM hr.leave_approvals a WHERE a.leave_id = NEW.id AND a.deleted_at IS NULL) THEN
                        NEW.status := 'PENDING_APPROVAL';
                        RETURN NEW;
                    END IF;

                    SELECT e.manager_employee_id INTO v_manager_employee_id
                    FROM hr.employees e
                    WHERE e.id = NEW.employee_id AND e.deleted_at IS NULL;

                    IF v_manager_employee_id IS NOT NULL THEN
                        SELECT l.user_id INTO v_manager_user_id
                        FROM system.user_employee_links l
                        JOIN system.users u ON u.id = l.user_id
                        WHERE l.employee_id = v_manager_employee_id
                          AND l.is_active = TRUE
                          AND l.deleted_at IS NULL
                          AND u.is_active = TRUE
                          AND u.deleted_at IS NULL
                        LIMIT 1;

                        v_manager_approval_id := gen_random_uuid();
                        INSERT INTO hr.leave_approvals (
                            id, leave_id, step_order, step_code, approver_employee_id, assigned_user_id,
                            status, created_at, created_by, version)
                        VALUES (
                            v_manager_approval_id, NEW.id, 1, 'MANAGER', v_manager_employee_id, v_manager_user_id,
                            'PENDING', now(), NEW.updated_by, 1);

                        INSERT INTO hr.leave_approval_history (
                            id, leave_id, approval_id, action, step_code, from_status, to_status, actor_user_id, occurred_at, note)
                        VALUES (
                            gen_random_uuid(), NEW.id, v_manager_approval_id, 'STEP_ACTIVATED', 'MANAGER', 'WAITING', 'PENDING', NEW.updated_by, now(),
                            CASE WHEN v_manager_user_id IS NULL THEN 'Yönetici personel tanımlı; sistem kullanıcı eşlemesi bekleniyor.' ELSE NULL END);

                        v_hr_approval_id := gen_random_uuid();
                        INSERT INTO hr.leave_approvals (
                            id, leave_id, step_order, step_code, approver_employee_id, assigned_user_id,
                            status, created_at, created_by, version)
                        VALUES (
                            v_hr_approval_id, NEW.id, 2, 'HR', NULL, NULL,
                            'WAITING', now(), NEW.updated_by, 1);
                    ELSE
                        v_manager_approval_id := gen_random_uuid();
                        INSERT INTO hr.leave_approvals (
                            id, leave_id, step_order, step_code, approver_employee_id, assigned_user_id,
                            status, decided_by_user_id, decided_at, decision_note, created_at, created_by, version)
                        VALUES (
                            v_manager_approval_id, NEW.id, 1, 'MANAGER', NULL, NULL,
                            'SKIPPED', NEW.updated_by, now(), 'Personel için yönetici tanımlı olmadığı için yönetici adımı atlandı.', now(), NEW.updated_by, 1);

                        INSERT INTO hr.leave_approval_history (
                            id, leave_id, approval_id, action, step_code, from_status, to_status, actor_user_id, occurred_at, note)
                        VALUES (
                            gen_random_uuid(), NEW.id, v_manager_approval_id, 'STEP_SKIPPED', 'MANAGER', 'WAITING', 'SKIPPED', NEW.updated_by, now(),
                            'Personel için yönetici tanımlı değil.');

                        v_hr_approval_id := gen_random_uuid();
                        INSERT INTO hr.leave_approvals (
                            id, leave_id, step_order, step_code, approver_employee_id, assigned_user_id,
                            status, created_at, created_by, version)
                        VALUES (
                            v_hr_approval_id, NEW.id, 2, 'HR', NULL, NULL,
                            'PENDING', now(), NEW.updated_by, 1);

                        INSERT INTO hr.leave_approval_history (
                            id, leave_id, approval_id, action, step_code, from_status, to_status, actor_user_id, occurred_at)
                        VALUES (
                            gen_random_uuid(), NEW.id, v_hr_approval_id, 'STEP_ACTIVATED', 'HR', 'WAITING', 'PENDING', NEW.updated_by, now());
                    END IF;

                    INSERT INTO hr.leave_approval_history (
                        id, leave_id, approval_id, action, step_code, from_status, to_status, actor_user_id, occurred_at)
                    VALUES (
                        gen_random_uuid(), NEW.id, NULL, 'WORKFLOW_STARTED', NULL, 'SUBMITTED', 'PENDING_APPROVAL', NEW.updated_by, now());

                    NEW.status := 'PENDING_APPROVAL';
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER trg_start_leave_approval_workflow
            BEFORE UPDATE OF status ON hr.leaves
            FOR EACH ROW
            EXECUTE FUNCTION hr.start_leave_approval_workflow();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000048', 'leave.manager.approve', 'Manager Leave Approval', 'Leave', 'Approve or reject manager approval steps for linked employee identity.', TRUE),
                ('20000000-0000-0000-0000-000000000049', 'leave.approver.manage', 'Manage Leave Approver Links', 'Leave', 'Manage system user to employee identity links used by approval workflows.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000048', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000048', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000049', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000049', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_start_leave_approval_workflow ON hr.leaves;
            DROP FUNCTION IF EXISTS hr.start_leave_approval_workflow();
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000048','30000000-0000-0000-0000-000000000049');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000048','20000000-0000-0000-0000-000000000049');
            DROP TABLE IF EXISTS hr.leave_approval_history;
            DROP TABLE IF EXISTS hr.leave_approvals;
            DROP TABLE IF EXISTS system.user_employee_links;
            """);
    }
}
