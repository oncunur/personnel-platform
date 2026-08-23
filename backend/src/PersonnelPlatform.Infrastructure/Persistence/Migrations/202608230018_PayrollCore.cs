using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230018_PayrollCore")]
public sealed class PayrollCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS payroll;
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE payroll.employee_compensations (
                id uuid NOT NULL CONSTRAINT pk_employee_compensations PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                valid_from date NOT NULL,
                valid_until_exclusive date NULL,
                monthly_base_salary numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                overtime_multiplier numeric(8,4) NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employee_compensations_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_compensations_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employee_compensations_dates CHECK (valid_until_exclusive IS NULL OR valid_until_exclusive > valid_from),
                CONSTRAINT ck_employee_compensations_month_boundary CHECK (EXTRACT(DAY FROM valid_from) = 1 AND (valid_until_exclusive IS NULL OR EXTRACT(DAY FROM valid_until_exclusive) = 1)),
                CONSTRAINT ck_employee_compensations_salary CHECK (monthly_base_salary > 0),
                CONSTRAINT ck_employee_compensations_currency CHECK (currency ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_employee_compensations_ot_multiplier CHECK (overtime_multiplier >= 1 AND overtime_multiplier <= 5)
            );
            CREATE INDEX ix_employee_compensations_employee_from ON payroll.employee_compensations(employee_id, valid_from DESC);
            ALTER TABLE payroll.employee_compensations
                ADD CONSTRAINT ex_employee_compensations_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    daterange(valid_from, COALESCE(valid_until_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL);

            CREATE TABLE payroll.periods (
                id uuid NOT NULL CONSTRAINT pk_payroll_periods PRIMARY KEY,
                company_id uuid NOT NULL,
                year integer NOT NULL,
                month integer NOT NULL,
                revision integer NOT NULL,
                previous_revision_id uuid NULL,
                status varchar(30) NOT NULL,
                calculation_version varchar(80) NOT NULL,
                calculated_at timestamptz NULL,
                calculated_by uuid NULL,
                approved_at timestamptz NULL,
                approved_by uuid NULL,
                closed_at timestamptz NULL,
                closed_by uuid NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_payroll_periods_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_payroll_periods_previous_revision FOREIGN KEY (previous_revision_id) REFERENCES payroll.periods(id) ON DELETE RESTRICT,
                CONSTRAINT ck_payroll_period_year CHECK (year BETWEEN 2000 AND 2200),
                CONSTRAINT ck_payroll_period_month CHECK (month BETWEEN 1 AND 12),
                CONSTRAINT ck_payroll_period_revision CHECK (revision > 0),
                CONSTRAINT ck_payroll_period_revision_link CHECK ((revision = 1 AND previous_revision_id IS NULL) OR (revision > 1 AND previous_revision_id IS NOT NULL)),
                CONSTRAINT ck_payroll_period_status CHECK (status IN ('DRAFT','OPEN','CALCULATING','CALCULATED','UNDER_REVIEW','APPROVED','CLOSED'))
            );
            CREATE UNIQUE INDEX ux_payroll_period_revision ON payroll.periods(company_id, year, month, revision) WHERE deleted_at IS NULL;
            CREATE INDEX ix_payroll_periods_company_status ON payroll.periods(company_id, status, year DESC, month DESC);

            CREATE TABLE payroll.employee_results (
                id uuid NOT NULL CONSTRAINT pk_payroll_employee_results PRIMARY KEY,
                payroll_period_id uuid NOT NULL,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                compensation_id uuid NOT NULL,
                monthly_base_salary_snapshot numeric(18,2) NOT NULL,
                currency_snapshot char(3) NOT NULL,
                overtime_multiplier_snapshot numeric(8,4) NOT NULL,
                planned_minutes integer NOT NULL,
                worked_minutes integer NOT NULL,
                paid_leave_minutes integer NOT NULL,
                approved_overtime_minutes integer NOT NULL,
                base_salary_amount numeric(18,2) NOT NULL,
                absence_deduction_amount numeric(18,2) NOT NULL,
                overtime_earning_amount numeric(18,2) NOT NULL,
                pay_before_statutory numeric(18,2) NOT NULL,
                meal_employer_cost numeric(18,2) NOT NULL,
                accommodation_employer_cost numeric(18,2) NOT NULL,
                employer_cost_before_statutory numeric(18,2) NOT NULL,
                source_snapshot_json jsonb NOT NULL,
                calculated_at timestamptz NOT NULL,
                CONSTRAINT fk_payroll_employee_results_period FOREIGN KEY (payroll_period_id) REFERENCES payroll.periods(id) ON DELETE RESTRICT,
                CONSTRAINT fk_payroll_employee_results_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_payroll_employee_results_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_payroll_employee_results_compensation FOREIGN KEY (compensation_id) REFERENCES payroll.employee_compensations(id) ON DELETE RESTRICT,
                CONSTRAINT ck_payroll_results_salary CHECK (monthly_base_salary_snapshot > 0),
                CONSTRAINT ck_payroll_results_currency CHECK (currency_snapshot ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_payroll_results_multiplier CHECK (overtime_multiplier_snapshot >= 1 AND overtime_multiplier_snapshot <= 5),
                CONSTRAINT ck_payroll_results_minutes CHECK (planned_minutes > 0 AND worked_minutes >= 0 AND paid_leave_minutes >= 0 AND approved_overtime_minutes >= 0),
                CONSTRAINT ck_payroll_results_amounts CHECK (base_salary_amount >= 0 AND absence_deduction_amount >= 0 AND overtime_earning_amount >= 0 AND pay_before_statutory >= 0 AND meal_employer_cost >= 0 AND accommodation_employer_cost >= 0 AND employer_cost_before_statutory >= 0)
            );
            CREATE UNIQUE INDEX ux_payroll_results_period_employee ON payroll.employee_results(payroll_period_id, employee_id);
            CREATE INDEX ix_payroll_results_company_employee ON payroll.employee_results(company_id, employee_id);

            CREATE OR REPLACE FUNCTION payroll.prevent_closed_period_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD.status = 'CLOSED' THEN
                    RAISE EXCEPTION 'PAYROLL_CLOSED_IMMUTABLE' USING ERRCODE = 'P0001';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_payroll_period_closed_immutable
            BEFORE UPDATE OR DELETE ON payroll.periods
            FOR EACH ROW EXECUTE FUNCTION payroll.prevent_closed_period_mutation();

            CREATE OR REPLACE FUNCTION payroll.prevent_closed_result_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE target_period uuid;
            BEGIN
                target_period := CASE WHEN TG_OP = 'DELETE' THEN OLD.payroll_period_id ELSE NEW.payroll_period_id END;
                IF EXISTS (SELECT 1 FROM payroll.periods WHERE id = target_period AND status = 'CLOSED') THEN
                    RAISE EXCEPTION 'PAYROLL_CLOSED_IMMUTABLE' USING ERRCODE = 'P0001';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_payroll_results_closed_immutable
            BEFORE INSERT OR UPDATE OR DELETE ON payroll.employee_results
            FOR EACH ROW EXECUTE FUNCTION payroll.prevent_closed_result_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000077', 'payroll.compensation.view', 'View Compensation', 'Payroll', 'View employee date-effective compensation definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000078', 'payroll.compensation.manage', 'Manage Compensation', 'Payroll', 'Create date-effective employee compensation definitions.', TRUE),
                ('20000000-0000-0000-0000-000000000079', 'payroll.period.view', 'View Payroll Periods', 'Payroll', 'View payroll periods and calculated employee results.', TRUE),
                ('20000000-0000-0000-0000-000000000080', 'payroll.period.manage', 'Manage Payroll Periods', 'Payroll', 'Create and open payroll period revisions.', TRUE),
                ('20000000-0000-0000-0000-000000000081', 'payroll.calculate', 'Calculate Payroll', 'Payroll', 'Calculate payroll from approved source snapshots.', TRUE),
                ('20000000-0000-0000-0000-000000000082', 'payroll.review', 'Review Payroll', 'Payroll', 'Move calculated payroll into review.', TRUE),
                ('20000000-0000-0000-0000-000000000083', 'payroll.approve', 'Approve Payroll', 'Payroll', 'Approve reviewed payroll periods.', TRUE),
                ('20000000-0000-0000-0000-000000000084', 'payroll.close', 'Close Payroll', 'Payroll', 'Close payroll periods and make them immutable.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000077', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000077', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000078', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000078', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000079', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000079', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000080', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000080', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000081', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000081', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000082', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000082', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000083', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000083', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000084', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000084', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000077','30000000-0000-0000-0000-000000000078','30000000-0000-0000-0000-000000000079','30000000-0000-0000-0000-000000000080',
                '30000000-0000-0000-0000-000000000081','30000000-0000-0000-0000-000000000082','30000000-0000-0000-0000-000000000083','30000000-0000-0000-0000-000000000084');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000077','20000000-0000-0000-0000-000000000078','20000000-0000-0000-0000-000000000079','20000000-0000-0000-0000-000000000080',
                '20000000-0000-0000-0000-000000000081','20000000-0000-0000-0000-000000000082','20000000-0000-0000-0000-000000000083','20000000-0000-0000-0000-000000000084');
            DROP TRIGGER IF EXISTS trg_payroll_results_closed_immutable ON payroll.employee_results;
            DROP FUNCTION IF EXISTS payroll.prevent_closed_result_mutation();
            DROP TRIGGER IF EXISTS trg_payroll_period_closed_immutable ON payroll.periods;
            DROP FUNCTION IF EXISTS payroll.prevent_closed_period_mutation();
            DROP TABLE IF EXISTS payroll.employee_results;
            DROP TABLE IF EXISTS payroll.periods;
            DROP TABLE IF EXISTS payroll.employee_compensations;
            DROP SCHEMA IF EXISTS payroll;
            """);
    }
}
