using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230013_AttendanceCalendarShiftCore")]
public sealed class AttendanceCalendarShiftCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS attendance;
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE attendance.work_calendars (
                id uuid NOT NULL CONSTRAINT pk_work_calendars PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                is_default boolean NOT NULL DEFAULT FALSE,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_work_calendars_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_work_calendars_company_code ON attendance.work_calendars(company_id, code);
            CREATE UNIQUE INDEX ux_work_calendars_default_company ON attendance.work_calendars(company_id)
                WHERE is_default = TRUE AND is_active = TRUE AND deleted_at IS NULL;

            CREATE TABLE attendance.work_calendar_days (
                id uuid NOT NULL CONSTRAINT pk_work_calendar_days PRIMARY KEY,
                work_calendar_id uuid NOT NULL,
                date date NOT NULL,
                day_type varchar(30) NOT NULL,
                planned_minutes integer NOT NULL DEFAULT 0,
                is_paid boolean NOT NULL DEFAULT TRUE,
                description varchar(500) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_work_calendar_days_calendar FOREIGN KEY (work_calendar_id) REFERENCES attendance.work_calendars(id) ON DELETE CASCADE,
                CONSTRAINT ck_work_calendar_days_type CHECK (day_type IN ('WORKDAY','WEEKEND','HOLIDAY','OFF_DAY')),
                CONSTRAINT ck_work_calendar_days_minutes CHECK (planned_minutes >= 0 AND planned_minutes <= 1440),
                CONSTRAINT ck_work_calendar_days_nonwork_minutes CHECK (day_type = 'WORKDAY' OR planned_minutes = 0)
            );
            CREATE UNIQUE INDEX ux_work_calendar_days_calendar_date ON attendance.work_calendar_days(work_calendar_id, date);

            CREATE TABLE attendance.shifts (
                id uuid NOT NULL CONSTRAINT pk_shifts PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                start_time time NOT NULL,
                end_time time NOT NULL,
                break_minutes integer NOT NULL DEFAULT 0,
                planned_minutes integer NOT NULL,
                grace_in_minutes integer NOT NULL DEFAULT 0,
                grace_out_minutes integer NOT NULL DEFAULT 0,
                crosses_midnight boolean NOT NULL DEFAULT FALSE,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_shifts_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_shifts_break CHECK (break_minutes >= 0),
                CONSTRAINT ck_shifts_planned CHECK (planned_minutes > 0 AND planned_minutes <= 1440),
                CONSTRAINT ck_shifts_grace CHECK (grace_in_minutes BETWEEN 0 AND 240 AND grace_out_minutes BETWEEN 0 AND 240),
                CONSTRAINT ck_shifts_times CHECK (start_time <> end_time)
            );
            CREATE UNIQUE INDEX ux_shifts_company_code ON attendance.shifts(company_id, code);

            CREATE TABLE attendance.employee_shift_assignments (
                id uuid NOT NULL CONSTRAINT pk_employee_shift_assignments PRIMARY KEY,
                employee_id uuid NOT NULL,
                shift_id uuid NOT NULL,
                work_calendar_id uuid NOT NULL,
                valid_from date NOT NULL,
                valid_until date NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employee_shift_assignments_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_shift_assignments_shift FOREIGN KEY (shift_id) REFERENCES attendance.shifts(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_shift_assignments_calendar FOREIGN KEY (work_calendar_id) REFERENCES attendance.work_calendars(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employee_shift_assignments_dates CHECK (valid_until IS NULL OR valid_until >= valid_from)
            );
            CREATE INDEX ix_employee_shift_assignments_employee_dates ON attendance.employee_shift_assignments(employee_id, valid_from, valid_until);
            CREATE INDEX ix_employee_shift_assignments_shift ON attendance.employee_shift_assignments(shift_id);
            CREATE INDEX ix_employee_shift_assignments_calendar ON attendance.employee_shift_assignments(work_calendar_id);
            ALTER TABLE attendance.employee_shift_assignments
                ADD CONSTRAINT ex_employee_shift_assignments_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    daterange(valid_from, COALESCE(valid_until, 'infinity'::date), '[]') WITH &&
                ) WHERE (deleted_at IS NULL);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000052', 'attendance.calendar.view', 'View Work Calendars', 'Attendance', 'View work calendars and calendar days in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000053', 'attendance.calendar.manage', 'Manage Work Calendars', 'Attendance', 'Create work calendars and maintain calendar-day overrides.', TRUE),
                ('20000000-0000-0000-0000-000000000054', 'attendance.shift.view', 'View Shifts', 'Attendance', 'View shift definitions in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000055', 'attendance.shift.manage', 'Manage Shifts', 'Attendance', 'Create and manage shift definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000056', 'attendance.assignment.view', 'View Shift Assignments', 'Attendance', 'View personnel shift assignment history.', TRUE),
                ('20000000-0000-0000-0000-000000000057', 'attendance.assignment.manage', 'Manage Shift Assignments', 'Attendance', 'Assign date-effective shifts and work calendars to personnel.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000052', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000052', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000053', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000053', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000054', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000054', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000055', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000055', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000056', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000056', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000057', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000057', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000052','30000000-0000-0000-0000-000000000053','30000000-0000-0000-0000-000000000054',
                '30000000-0000-0000-0000-000000000055','30000000-0000-0000-0000-000000000056','30000000-0000-0000-0000-000000000057');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000052','20000000-0000-0000-0000-000000000053','20000000-0000-0000-0000-000000000054',
                '20000000-0000-0000-0000-000000000055','20000000-0000-0000-0000-000000000056','20000000-0000-0000-0000-000000000057');
            DROP TABLE IF EXISTS attendance.employee_shift_assignments;
            DROP TABLE IF EXISTS attendance.shifts;
            DROP TABLE IF EXISTS attendance.work_calendar_days;
            DROP TABLE IF EXISTS attendance.work_calendars;
            DROP SCHEMA IF EXISTS attendance;
            """);
    }
}
