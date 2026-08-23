using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230024_FinanceReporting")]
public sealed class FinanceReporting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS finance;
            CREATE SCHEMA IF NOT EXISTS reporting;

            CREATE TABLE finance.cost_entries (
                id uuid NOT NULL CONSTRAINT pk_finance_cost_entries PRIMARY KEY,
                company_id uuid NOT NULL,
                source_type varchar(40) NOT NULL,
                source_id uuid NOT NULL,
                source_line_key varchar(220) NOT NULL,
                employee_id uuid NULL,
                project_id uuid NULL,
                cost_center_id uuid NULL,
                cost_date date NOT NULL,
                category varchar(50) NOT NULL,
                quantity numeric(18,4) NOT NULL,
                unit varchar(30) NOT NULL,
                amount numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                allocation_basis varchar(30) NOT NULL,
                metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_finance_cost_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_cost_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_cost_project FOREIGN KEY (project_id) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_cost_center FOREIGN KEY (cost_center_id) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT ck_finance_cost_source CHECK (source_type IN ('PAYROLL','MEAL','ACCOMMODATION')),
                CONSTRAINT ck_finance_cost_quantity CHECK (quantity >= 0),
                CONSTRAINT ck_finance_cost_amount CHECK (amount >= 0),
                CONSTRAINT ck_finance_cost_currency CHECK (char_length(currency) = 3),
                CONSTRAINT ck_finance_cost_allocation CHECK (allocation_basis IN ('DIRECT','ATTENDANCE','FIXED','MANUAL','UNALLOCATED')),
                CONSTRAINT ck_finance_cost_metadata CHECK (jsonb_typeof(metadata_json) = 'object')
            );
            CREATE UNIQUE INDEX ux_finance_cost_source_line ON finance.cost_entries(source_type, source_id, source_line_key);
            CREATE INDEX ix_finance_cost_company_date ON finance.cost_entries(company_id, cost_date DESC);
            CREATE INDEX ix_finance_cost_project_date ON finance.cost_entries(project_id, cost_date DESC) WHERE project_id IS NOT NULL;
            CREATE INDEX ix_finance_cost_employee_date ON finance.cost_entries(employee_id, cost_date DESC) WHERE employee_id IS NOT NULL;
            CREATE INDEX ix_finance_cost_center_date ON finance.cost_entries(cost_center_id, cost_date DESC) WHERE cost_center_id IS NOT NULL;

            CREATE OR REPLACE FUNCTION finance.prevent_cost_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'FINANCE_COST_LEDGER_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_finance_cost_immutable BEFORE UPDATE OR DELETE ON finance.cost_entries FOR EACH ROW EXECUTE FUNCTION finance.prevent_cost_mutation();

            CREATE TABLE finance.payroll_allocation_overrides (
                id uuid NOT NULL CONSTRAINT pk_finance_payroll_allocation_overrides PRIMARY KEY,
                payroll_period_id uuid NOT NULL,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                project_id uuid NOT NULL,
                cost_center_id uuid NULL,
                allocation_percent numeric(8,4) NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_finance_allocation_payroll_period FOREIGN KEY (payroll_period_id) REFERENCES payroll.periods(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_allocation_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_allocation_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_allocation_project FOREIGN KEY (project_id) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_finance_allocation_cost_center FOREIGN KEY (cost_center_id) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT ck_finance_allocation_percent CHECK (allocation_percent > 0 AND allocation_percent <= 100)
            );
            CREATE UNIQUE INDEX ux_finance_allocation_period_employee_target
                ON finance.payroll_allocation_overrides(payroll_period_id, employee_id, project_id, COALESCE(cost_center_id, '00000000-0000-0000-0000-000000000000'::uuid));
            CREATE INDEX ix_finance_allocation_period_employee ON finance.payroll_allocation_overrides(payroll_period_id, employee_id);

            CREATE TABLE reporting.export_jobs (
                id uuid NOT NULL CONSTRAINT pk_reporting_export_jobs PRIMARY KEY,
                company_id uuid NOT NULL,
                requested_by_user_id uuid NOT NULL,
                report_type varchar(50) NOT NULL,
                format varchar(10) NOT NULL,
                filters_json jsonb NOT NULL,
                status varchar(30) NOT NULL,
                storage_key varchar(500) NULL,
                file_name varchar(240) NULL,
                content_type varchar(150) NULL,
                file_size_bytes bigint NULL,
                started_at timestamptz NULL,
                completed_at timestamptz NULL,
                error_message varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_reporting_export_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_reporting_export_user FOREIGN KEY (requested_by_user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_reporting_export_type CHECK (report_type IN ('COST_LEDGER','PROJECT_360','MANAGEMENT')),
                CONSTRAINT ck_reporting_export_format CHECK (format IN ('XLSX','PDF')),
                CONSTRAINT ck_reporting_export_status CHECK (status IN ('QUEUED','PROCESSING','COMPLETED','FAILED')),
                CONSTRAINT ck_reporting_export_filters CHECK (jsonb_typeof(filters_json) = 'object'),
                CONSTRAINT ck_reporting_export_size CHECK (file_size_bytes IS NULL OR file_size_bytes > 0),
                CONSTRAINT ck_reporting_export_completed CHECK (
                    status <> 'COMPLETED' OR (storage_key IS NOT NULL AND file_name IS NOT NULL AND content_type IS NOT NULL AND file_size_bytes IS NOT NULL AND completed_at IS NOT NULL)
                )
            );
            CREATE INDEX ix_reporting_export_user_time ON reporting.export_jobs(requested_by_user_id, created_at DESC);
            CREATE INDEX ix_reporting_export_status_time ON reporting.export_jobs(status, created_at);
            CREATE INDEX ix_reporting_export_company_time ON reporting.export_jobs(company_id, created_at DESC);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000116', 'finance.cost.view', 'View Cost Ledger', 'Finance', 'View centralized immutable cost ledger within scope.', TRUE),
                ('20000000-0000-0000-0000-000000000117', 'finance.cost.process', 'Process Cost Ledger', 'Finance', 'Synchronize eligible payroll, meal and accommodation sources to cost ledger.', TRUE),
                ('20000000-0000-0000-0000-000000000118', 'finance.allocation.view', 'View Payroll Cost Allocation', 'Finance', 'View payroll project and cost center allocation overrides.', TRUE),
                ('20000000-0000-0000-0000-000000000119', 'finance.allocation.manage', 'Manage Payroll Cost Allocation', 'Finance', 'Configure payroll project and cost center allocation before ledger posting.', TRUE),
                ('20000000-0000-0000-0000-000000000120', 'reporting.view', 'View Reports', 'Reporting', 'View report center, Project 360 and management KPI dashboards.', TRUE),
                ('20000000-0000-0000-0000-000000000121', 'reporting.export', 'Export Reports', 'Reporting', 'Create and securely download background XLSX/PDF report exports.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000116', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000116', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000117', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000117', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000118', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000118', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000119', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000119', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000120', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000120', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000121', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000121', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000116','30000000-0000-0000-0000-000000000117','30000000-0000-0000-0000-000000000118',
                '30000000-0000-0000-0000-000000000119','30000000-0000-0000-0000-000000000120','30000000-0000-0000-0000-000000000121');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000116','20000000-0000-0000-0000-000000000117','20000000-0000-0000-0000-000000000118',
                '20000000-0000-0000-0000-000000000119','20000000-0000-0000-0000-000000000120','20000000-0000-0000-0000-000000000121');
            DROP TABLE IF EXISTS reporting.export_jobs;
            DROP SCHEMA IF EXISTS reporting;
            DROP TABLE IF EXISTS finance.payroll_allocation_overrides;
            DROP TRIGGER IF EXISTS trg_finance_cost_immutable ON finance.cost_entries;
            DROP FUNCTION IF EXISTS finance.prevent_cost_mutation();
            DROP TABLE IF EXISTS finance.cost_entries;
            DROP SCHEMA IF EXISTS finance;
            """);
    }
}
