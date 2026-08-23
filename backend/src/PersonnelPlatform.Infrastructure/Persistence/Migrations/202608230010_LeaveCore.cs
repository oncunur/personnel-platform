using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230010_LeaveCore")]
public sealed class LeaveCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE hr.leave_types (
                id uuid NOT NULL CONSTRAINT pk_leave_types PRIMARY KEY,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                description varchar(1000) NULL,
                is_paid boolean NOT NULL DEFAULT TRUE,
                balance_required boolean NOT NULL DEFAULT FALSE,
                allow_half_day boolean NOT NULL DEFAULT FALSE,
                attachment_required boolean NOT NULL DEFAULT FALSE,
                is_active boolean NOT NULL DEFAULT TRUE,
                display_order integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX ux_leave_types_code ON hr.leave_types(code);

            CREATE TABLE hr.leave_entitlements (
                id uuid NOT NULL CONSTRAINT pk_leave_entitlements PRIMARY KEY,
                employee_id uuid NOT NULL,
                leave_type_id uuid NOT NULL,
                period_start date NOT NULL,
                period_end date NOT NULL,
                entitled_days numeric(10,2) NOT NULL DEFAULT 0,
                carry_over_days numeric(10,2) NOT NULL DEFAULT 0,
                adjustment_days numeric(10,2) NOT NULL DEFAULT 0,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_leave_entitlements_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_leave_entitlements_type FOREIGN KEY (leave_type_id) REFERENCES hr.leave_types(id) ON DELETE RESTRICT,
                CONSTRAINT ck_leave_entitlements_dates CHECK (period_end >= period_start),
                CONSTRAINT ck_leave_entitlements_amounts CHECK (entitled_days >= 0 AND carry_over_days >= 0)
            );
            CREATE UNIQUE INDEX ux_leave_entitlements_period ON hr.leave_entitlements(employee_id, leave_type_id, period_start, period_end);
            ALTER TABLE hr.leave_entitlements
                ADD CONSTRAINT ex_leave_entitlements_period_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    leave_type_id WITH =,
                    daterange(period_start, period_end, '[]') WITH &&
                ) WHERE (deleted_at IS NULL);

            CREATE TABLE hr.leave_balances (
                id uuid NOT NULL CONSTRAINT pk_leave_balances PRIMARY KEY,
                employee_id uuid NOT NULL,
                leave_type_id uuid NOT NULL,
                period_start date NOT NULL,
                period_end date NOT NULL,
                entitled_days numeric(10,2) NOT NULL DEFAULT 0,
                carry_over_days numeric(10,2) NOT NULL DEFAULT 0,
                adjustment_days numeric(10,2) NOT NULL DEFAULT 0,
                reserved_days numeric(10,2) NOT NULL DEFAULT 0,
                used_days numeric(10,2) NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_leave_balances_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_leave_balances_type FOREIGN KEY (leave_type_id) REFERENCES hr.leave_types(id) ON DELETE RESTRICT,
                CONSTRAINT ck_leave_balances_dates CHECK (period_end >= period_start),
                CONSTRAINT ck_leave_balances_nonnegative CHECK (entitled_days >= 0 AND carry_over_days >= 0 AND reserved_days >= 0 AND used_days >= 0)
            );
            CREATE UNIQUE INDEX ux_leave_balances_period ON hr.leave_balances(employee_id, leave_type_id, period_start, period_end);
            CREATE INDEX ix_leave_balances_employee_period ON hr.leave_balances(employee_id, period_start, period_end);

            CREATE TABLE hr.leaves (
                id uuid NOT NULL CONSTRAINT pk_leaves PRIMARY KEY,
                employee_id uuid NOT NULL,
                leave_type_id uuid NOT NULL,
                start_date date NOT NULL,
                end_date date NOT NULL,
                start_day_part varchar(20) NOT NULL DEFAULT 'FULL_DAY',
                end_day_part varchar(20) NOT NULL DEFAULT 'FULL_DAY',
                requested_days numeric(10,2) NOT NULL,
                reason varchar(2000) NULL,
                status varchar(30) NOT NULL DEFAULT 'DRAFT',
                submitted_at timestamptz NULL,
                withdrawn_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_leaves_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_leaves_type FOREIGN KEY (leave_type_id) REFERENCES hr.leave_types(id) ON DELETE RESTRICT,
                CONSTRAINT ck_leaves_dates CHECK (end_date >= start_date),
                CONSTRAINT ck_leaves_requested_days CHECK (requested_days > 0),
                CONSTRAINT ck_leaves_start_day_part CHECK (start_day_part IN ('FULL_DAY','FIRST_HALF','SECOND_HALF')),
                CONSTRAINT ck_leaves_end_day_part CHECK (end_day_part IN ('FULL_DAY','FIRST_HALF','SECOND_HALF')),
                CONSTRAINT ck_leaves_status CHECK (status IN ('DRAFT','SUBMITTED','PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED','WITHDRAWN','COMPLETED'))
            );
            CREATE INDEX ix_leaves_employee_dates ON hr.leaves(employee_id, start_date, end_date);
            CREATE INDEX ix_leaves_status_start ON hr.leaves(status, start_date);
            CREATE INDEX ix_leaves_type_start ON hr.leaves(leave_type_id, start_date);

            ALTER TABLE hr.leaves
                ADD CONSTRAINT ex_leaves_employee_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    tsrange(
                        start_date::timestamp + CASE WHEN start_day_part = 'SECOND_HALF' THEN interval '12 hours' ELSE interval '0 hours' END,
                        end_date::timestamp + CASE WHEN end_day_part = 'FIRST_HALF' THEN interval '12 hours' ELSE interval '1 day' END,
                        '[)'
                    ) WITH &&
                ) WHERE (status IN ('SUBMITTED','PENDING_APPROVAL','APPROVED','COMPLETED') AND deleted_at IS NULL);

            INSERT INTO hr.leave_types (id, code, name, description, is_paid, balance_required, allow_half_day, attachment_required, is_active, display_order, created_at, version) VALUES
                ('42000000-0000-0000-0000-000000000001', 'ANNUAL_LEAVE', 'Yıllık İzin', 'Bakiye takipli yıllık izin.', TRUE, TRUE, TRUE, FALSE, TRUE, 10, '2026-08-23T00:00:00Z', 1),
                ('42000000-0000-0000-0000-000000000002', 'UNPAID_LEAVE', 'Ücretsiz İzin', 'Bakiye takibi gerektirmeyen ücretsiz izin.', FALSE, FALSE, FALSE, FALSE, TRUE, 20, '2026-08-23T00:00:00Z', 1),
                ('42000000-0000-0000-0000-000000000003', 'SICK_LEAVE', 'Hastalık İzni', 'Şirket kuralına göre ücret ve belge koşulları düzenlenebilir.', FALSE, FALSE, TRUE, TRUE, TRUE, 30, '2026-08-23T00:00:00Z', 1);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000040', 'leave.type.view', 'View Leave Types', 'Leave', 'View leave type definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000041', 'leave.type.manage', 'Manage Leave Types', 'Leave', 'Create and manage leave type definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000042', 'leave.view', 'View Leave Requests', 'Leave', 'View leave requests in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000043', 'leave.create', 'Create Leave Requests', 'Leave', 'Create leave requests for authorized personnel.', TRUE),
                ('20000000-0000-0000-0000-000000000044', 'leave.submit', 'Submit Leave Requests', 'Leave', 'Submit and withdraw leave requests.', TRUE),
                ('20000000-0000-0000-0000-000000000045', 'leave.balance.view', 'View Leave Balances', 'Leave', 'View leave balances in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000046', 'leave.balance.manage', 'Manage Leave Entitlements', 'Leave', 'Create and adjust leave entitlements and balances.', TRUE),
                ('20000000-0000-0000-0000-000000000047', 'leave.approve', 'Approve Leave Requests', 'Leave', 'Reserved for leave approval workflow.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000040', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000040', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000041', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000041', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000042', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000042', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000043', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000043', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000044', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000044', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000045', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000045', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000046', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000046', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000047', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000047', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000040','30000000-0000-0000-0000-000000000041','30000000-0000-0000-0000-000000000042','30000000-0000-0000-0000-000000000043',
                '30000000-0000-0000-0000-000000000044','30000000-0000-0000-0000-000000000045','30000000-0000-0000-0000-000000000046','30000000-0000-0000-0000-000000000047');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000040','20000000-0000-0000-0000-000000000041','20000000-0000-0000-0000-000000000042','20000000-0000-0000-0000-000000000043',
                '20000000-0000-0000-0000-000000000044','20000000-0000-0000-0000-000000000045','20000000-0000-0000-0000-000000000046','20000000-0000-0000-0000-000000000047');
            DROP TABLE IF EXISTS hr.leaves;
            DROP TABLE IF EXISTS hr.leave_balances;
            DROP TABLE IF EXISTS hr.leave_entitlements;
            DROP TABLE IF EXISTS hr.leave_types;
            """);
    }
}
