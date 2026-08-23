using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230015_OvertimeCore")]
public sealed class OvertimeCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE attendance.overtime_requests (
                id uuid NOT NULL CONSTRAINT pk_overtime_requests PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                daily_attendance_id uuid NOT NULL,
                source_daily_version integer NOT NULL,
                attendance_date date NOT NULL,
                candidate_minutes integer NOT NULL,
                requested_minutes integer NOT NULL,
                approved_minutes integer NOT NULL DEFAULT 0,
                status varchar(30) NOT NULL,
                reason varchar(2000) NULL,
                submitted_at timestamptz NOT NULL,
                manager_decided_at timestamptz NULL,
                manager_decided_by uuid NULL,
                hr_decided_at timestamptz NULL,
                hr_decided_by uuid NULL,
                rejected_at timestamptz NULL,
                rejected_by uuid NULL,
                decision_note varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_overtime_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_overtime_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_overtime_daily FOREIGN KEY (daily_attendance_id) REFERENCES attendance.daily_attendance(id) ON DELETE RESTRICT,
                CONSTRAINT ck_overtime_status CHECK (status IN ('PENDING_MANAGER','PENDING_HR','APPROVED','REJECTED','CANCELLED')),
                CONSTRAINT ck_overtime_source_version CHECK (source_daily_version > 0),
                CONSTRAINT ck_overtime_candidate_minutes CHECK (candidate_minutes > 0),
                CONSTRAINT ck_overtime_requested_minutes CHECK (requested_minutes > 0 AND requested_minutes <= candidate_minutes),
                CONSTRAINT ck_overtime_approved_minutes CHECK (approved_minutes >= 0 AND approved_minutes <= requested_minutes),
                CONSTRAINT ck_overtime_approved_state CHECK ((status = 'APPROVED' AND approved_minutes > 0) OR (status <> 'APPROVED' AND approved_minutes = 0))
            );

            CREATE INDEX ix_overtime_employee_date ON attendance.overtime_requests(employee_id, attendance_date DESC);
            CREATE INDEX ix_overtime_company_status_date ON attendance.overtime_requests(company_id, status, attendance_date);
            CREATE UNIQUE INDEX ux_overtime_active_daily
                ON attendance.overtime_requests(daily_attendance_id)
                WHERE deleted_at IS NULL AND status NOT IN ('REJECTED','CANCELLED');

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000062', 'attendance.overtime.view', 'View Overtime Requests', 'Attendance', 'View overtime candidates, requests and approved minutes in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000063', 'attendance.overtime.request', 'Create Overtime Requests', 'Attendance', 'Create overtime requests from daily attendance candidate minutes.', TRUE),
                ('20000000-0000-0000-0000-000000000064', 'attendance.overtime.manager.approve', 'Manager Overtime Approval', 'Attendance', 'Approve or reject overtime requests for direct reports.', TRUE),
                ('20000000-0000-0000-0000-000000000065', 'attendance.overtime.hr.approve', 'HR Overtime Approval', 'Attendance', 'Perform final HR approval and determine approved overtime minutes.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000062', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000062', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000063', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000063', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000064', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000064', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000065', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000065', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000062','30000000-0000-0000-0000-000000000063',
                '30000000-0000-0000-0000-000000000064','30000000-0000-0000-0000-000000000065');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000062','20000000-0000-0000-0000-000000000063',
                '20000000-0000-0000-0000-000000000064','20000000-0000-0000-0000-000000000065');
            DROP TABLE IF EXISTS attendance.overtime_requests;
            """);
    }
}
