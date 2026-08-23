using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230014_AttendanceRawDaily")]
public sealed class AttendanceRawDaily : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE attendance.raw_events (
                id uuid NOT NULL CONSTRAINT pk_raw_events PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                source varchar(30) NOT NULL,
                direction varchar(20) NOT NULL,
                event_at timestamptz NOT NULL,
                local_date date NOT NULL,
                local_time time NOT NULL,
                utc_offset_minutes integer NOT NULL,
                device_code varchar(100) NULL,
                external_event_id varchar(200) NULL,
                raw_payload_json text NULL,
                received_at timestamptz NOT NULL,
                received_by uuid NOT NULL,
                CONSTRAINT fk_raw_events_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_raw_events_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_raw_events_received_by FOREIGN KEY (received_by) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_raw_events_source CHECK (source IN ('PDKS','MANUAL','IMPORT','INTEGRATION')),
                CONSTRAINT ck_raw_events_direction CHECK (direction IN ('IN','OUT','UNKNOWN')),
                CONSTRAINT ck_raw_events_offset CHECK (utc_offset_minutes BETWEEN -840 AND 840),
                CONSTRAINT ck_raw_events_external_id CHECK (source = 'MANUAL' OR external_event_id IS NOT NULL)
            );
            CREATE INDEX ix_raw_events_employee_local ON attendance.raw_events(employee_id, local_date, local_time);
            CREATE INDEX ix_raw_events_company_local ON attendance.raw_events(company_id, local_date);
            CREATE UNIQUE INDEX ux_raw_attendance_events_company_source_external
                ON attendance.raw_events(company_id, source, external_event_id)
                WHERE external_event_id IS NOT NULL;

            CREATE OR REPLACE FUNCTION attendance.prevent_raw_event_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'RAW_ATTENDANCE_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;

            CREATE TRIGGER trg_raw_events_immutable
            BEFORE UPDATE OR DELETE ON attendance.raw_events
            FOR EACH ROW
            EXECUTE FUNCTION attendance.prevent_raw_event_mutation();

            CREATE TABLE attendance.daily_attendance (
                id uuid NOT NULL CONSTRAINT pk_daily_attendance PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                attendance_date date NOT NULL,
                shift_assignment_id uuid NULL,
                shift_id uuid NULL,
                work_calendar_id uuid NULL,
                leave_id uuid NULL,
                status varchar(30) NOT NULL,
                processing_status varchar(30) NOT NULL,
                planned_minutes integer NOT NULL DEFAULT 0,
                leave_minutes integer NOT NULL DEFAULT 0,
                worked_minutes integer NOT NULL DEFAULT 0,
                late_minutes integer NOT NULL DEFAULT 0,
                early_leave_minutes integer NOT NULL DEFAULT 0,
                overtime_candidate_minutes integer NOT NULL DEFAULT 0,
                first_in_at timestamptz NULL,
                last_out_at timestamptz NULL,
                source_snapshot_json jsonb NOT NULL,
                calculation_message varchar(2000) NULL,
                calculated_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_daily_attendance_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_daily_attendance_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_daily_attendance_assignment FOREIGN KEY (shift_assignment_id) REFERENCES attendance.employee_shift_assignments(id) ON DELETE RESTRICT,
                CONSTRAINT fk_daily_attendance_shift FOREIGN KEY (shift_id) REFERENCES attendance.shifts(id) ON DELETE RESTRICT,
                CONSTRAINT fk_daily_attendance_calendar FOREIGN KEY (work_calendar_id) REFERENCES attendance.work_calendars(id) ON DELETE RESTRICT,
                CONSTRAINT fk_daily_attendance_leave FOREIGN KEY (leave_id) REFERENCES hr.leaves(id) ON DELETE RESTRICT,
                CONSTRAINT ck_daily_attendance_status CHECK (status IN ('WORKED','PARTIAL','LEAVE','SICK','ABSENT','HOLIDAY','OFF_DAY','MISSING_RECORD')),
                CONSTRAINT ck_daily_attendance_processing CHECK (processing_status IN ('CALCULATED','REVIEW_REQUIRED','APPROVED','LOCKED')),
                CONSTRAINT ck_daily_attendance_minutes CHECK (
                    planned_minutes >= 0 AND leave_minutes >= 0 AND worked_minutes >= 0 AND
                    late_minutes >= 0 AND early_leave_minutes >= 0 AND overtime_candidate_minutes >= 0)
            );
            CREATE UNIQUE INDEX ux_daily_attendance_employee_date ON attendance.daily_attendance(employee_id, attendance_date) WHERE deleted_at IS NULL;
            CREATE INDEX ix_daily_attendance_company_date_status ON attendance.daily_attendance(company_id, attendance_date, processing_status);
            CREATE INDEX ix_daily_attendance_assignment ON attendance.daily_attendance(shift_assignment_id);
            CREATE INDEX ix_daily_attendance_leave ON attendance.daily_attendance(leave_id);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000058', 'attendance.raw.view', 'View Raw Attendance Events', 'Attendance', 'View immutable PDKS/raw attendance events in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000059', 'attendance.raw.ingest', 'Ingest Raw Attendance Events', 'Attendance', 'Append PDKS/manual/import attendance events without mutating source history.', TRUE),
                ('20000000-0000-0000-0000-000000000060', 'attendance.daily.view', 'View Daily Attendance', 'Attendance', 'View calculated daily attendance in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000061', 'attendance.daily.calculate', 'Calculate Daily Attendance', 'Attendance', 'Calculate or recalculate unlocked daily attendance from schedule, leave and raw events.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000058', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000058', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000059', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000059', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000060', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000060', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000061', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000061', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000058','30000000-0000-0000-0000-000000000059',
                '30000000-0000-0000-0000-000000000060','30000000-0000-0000-0000-000000000061');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000058','20000000-0000-0000-0000-000000000059',
                '20000000-0000-0000-0000-000000000060','20000000-0000-0000-0000-000000000061');
            DROP TABLE IF EXISTS attendance.daily_attendance;
            DROP TRIGGER IF EXISTS trg_raw_events_immutable ON attendance.raw_events;
            DROP FUNCTION IF EXISTS attendance.prevent_raw_event_mutation();
            DROP TABLE IF EXISTS attendance.raw_events;
            """);
    }
}
