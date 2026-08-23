using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230026_ImportErp")]
public sealed class ImportErp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE integration.import_jobs (
                id uuid NOT NULL CONSTRAINT pk_integration_import_jobs PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                target_type varchar(50) NOT NULL,
                original_file_name varchar(240) NOT NULL,
                file_hash varchar(128) NOT NULL,
                status varchar(30) NOT NULL,
                headers_json jsonb NOT NULL,
                mapping_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                total_rows integer NOT NULL,
                success_rows integer NOT NULL DEFAULT 0,
                error_rows integer NOT NULL DEFAULT 0,
                completed_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_import_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_import_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_import_target CHECK (target_type IN ('INTEGRATION_MAPPING','ERP_ACCOUNT_MAPPING')),
                CONSTRAINT ck_integration_import_status CHECK (status IN ('UPLOADED','READY','PROCESSING','COMPLETED','PARTIAL','FAILED')),
                CONSTRAINT ck_integration_import_headers CHECK (jsonb_typeof(headers_json) = 'array'),
                CONSTRAINT ck_integration_import_mapping CHECK (jsonb_typeof(mapping_json) = 'object'),
                CONSTRAINT ck_integration_import_counts CHECK (total_rows >= 0 AND success_rows >= 0 AND error_rows >= 0 AND success_rows + error_rows <= total_rows)
            );
            CREATE INDEX ix_integration_import_company_time ON integration.import_jobs(company_id, created_at DESC);
            CREATE INDEX ix_integration_import_status_time ON integration.import_jobs(status, created_at DESC);

            CREATE TABLE integration.import_rows (
                id uuid NOT NULL CONSTRAINT pk_integration_import_rows PRIMARY KEY,
                import_job_id uuid NOT NULL,
                row_number integer NOT NULL,
                raw_json jsonb NOT NULL,
                status varchar(30) NOT NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                processed_entity_type varchar(60) NULL,
                processed_entity_id uuid NULL,
                processed_at timestamptz NULL,
                CONSTRAINT fk_integration_import_row_job FOREIGN KEY (import_job_id) REFERENCES integration.import_jobs(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_import_row_number CHECK (row_number >= 2),
                CONSTRAINT ck_integration_import_row_raw CHECK (jsonb_typeof(raw_json) = 'object'),
                CONSTRAINT ck_integration_import_row_status CHECK (status IN ('PREVIEW','IMPORTED','ERROR'))
            );
            CREATE UNIQUE INDEX ux_integration_import_row_number ON integration.import_rows(import_job_id, row_number);
            CREATE INDEX ix_integration_import_row_status ON integration.import_rows(import_job_id, status);

            CREATE OR REPLACE FUNCTION integration.prevent_import_raw_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD.company_id IS DISTINCT FROM NEW.company_id
                   OR OLD.integration_system_id IS DISTINCT FROM NEW.integration_system_id
                   OR OLD.target_type IS DISTINCT FROM NEW.target_type
                   OR OLD.original_file_name IS DISTINCT FROM NEW.original_file_name
                   OR OLD.file_hash IS DISTINCT FROM NEW.file_hash
                   OR OLD.headers_json IS DISTINCT FROM NEW.headers_json
                   OR OLD.total_rows IS DISTINCT FROM NEW.total_rows THEN
                    RAISE EXCEPTION 'INTEGRATION_IMPORT_SOURCE_IMMUTABLE' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_integration_import_source_immutable BEFORE UPDATE ON integration.import_jobs FOR EACH ROW EXECUTE FUNCTION integration.prevent_import_raw_mutation();

            CREATE OR REPLACE FUNCTION integration.prevent_import_row_raw_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD.import_job_id IS DISTINCT FROM NEW.import_job_id
                   OR OLD.row_number IS DISTINCT FROM NEW.row_number
                   OR OLD.raw_json IS DISTINCT FROM NEW.raw_json THEN
                    RAISE EXCEPTION 'INTEGRATION_IMPORT_ROW_RAW_IMMUTABLE' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_integration_import_row_raw_immutable BEFORE UPDATE ON integration.import_rows FOR EACH ROW EXECUTE FUNCTION integration.prevent_import_row_raw_mutation();

            CREATE TABLE integration.erp_account_mappings (
                id uuid NOT NULL CONSTRAINT pk_integration_erp_account_mappings PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                cost_category varchar(50) NOT NULL,
                account_code varchar(100) NOT NULL,
                counter_account_code varchar(100) NULL,
                is_active boolean NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_erp_account_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_erp_account_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_integration_erp_account_category ON integration.erp_account_mappings(integration_system_id, cost_category) WHERE deleted_at IS NULL;
            CREATE INDEX ix_integration_erp_account_company ON integration.erp_account_mappings(company_id, is_active);

            CREATE TABLE integration.erp_export_batches (
                id uuid NOT NULL CONSTRAINT pk_integration_erp_export_batches PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                from_date date NOT NULL,
                to_date date NOT NULL,
                status varchar(30) NOT NULL,
                sent_at timestamptz NULL,
                reconciled_at timestamptz NULL,
                closed_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_erp_batch_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_erp_batch_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_erp_batch_range CHECK (to_date >= from_date),
                CONSTRAINT ck_integration_erp_batch_status CHECK (status IN ('DRAFT','SENT','PARTIALLY_ACCEPTED','ACCEPTED','REJECTED','CLOSED')),
                CONSTRAINT ck_integration_erp_batch_close CHECK (status <> 'CLOSED' OR closed_at IS NOT NULL)
            );
            CREATE INDEX ix_integration_erp_batch_company_time ON integration.erp_export_batches(company_id, created_at DESC);
            CREATE INDEX ix_integration_erp_batch_system_status ON integration.erp_export_batches(integration_system_id, status);

            CREATE TABLE integration.erp_export_lines (
                id uuid NOT NULL CONSTRAINT pk_integration_erp_export_lines PRIMARY KEY,
                batch_id uuid NOT NULL,
                cost_entry_id uuid NOT NULL,
                external_line_key varchar(120) NOT NULL,
                source_type varchar(40) NOT NULL,
                source_id uuid NOT NULL,
                employee_id uuid NULL,
                project_id uuid NULL,
                cost_center_id uuid NULL,
                cost_date date NOT NULL,
                cost_category varchar(50) NOT NULL,
                account_code varchar(100) NOT NULL,
                counter_account_code varchar(100) NULL,
                sent_amount numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                status varchar(20) NOT NULL,
                accepted_amount numeric(18,2) NULL,
                variance_amount numeric(18,2) NULL,
                external_reference varchar(200) NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                reconciled_at timestamptz NULL,
                CONSTRAINT fk_integration_erp_line_batch FOREIGN KEY (batch_id) REFERENCES integration.erp_export_batches(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_erp_line_cost_entry FOREIGN KEY (cost_entry_id) REFERENCES finance.cost_entries(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_erp_line_status CHECK (status IN ('PENDING','SENT','ACCEPTED','REJECTED')),
                CONSTRAINT ck_integration_erp_line_amount CHECK (sent_amount >= 0 AND (accepted_amount IS NULL OR accepted_amount >= 0)),
                CONSTRAINT ck_integration_erp_line_currency CHECK (char_length(currency) = 3)
            );
            CREATE UNIQUE INDEX ux_integration_erp_line_cost ON integration.erp_export_lines(batch_id, cost_entry_id);
            CREATE UNIQUE INDEX ux_integration_erp_line_external ON integration.erp_export_lines(batch_id, external_line_key);
            CREATE INDEX ix_integration_erp_line_cost_status ON integration.erp_export_lines(cost_entry_id, status);

            CREATE TABLE integration.erp_reconciliation_events (
                id uuid NOT NULL CONSTRAINT pk_integration_erp_reconciliation_events PRIMARY KEY,
                batch_id uuid NOT NULL,
                line_id uuid NOT NULL,
                status varchar(20) NOT NULL,
                sent_amount numeric(18,2) NOT NULL,
                accepted_amount numeric(18,2) NULL,
                variance_amount numeric(18,2) NOT NULL,
                external_reference varchar(200) NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                actor_user_id uuid NOT NULL,
                occurred_at timestamptz NOT NULL,
                CONSTRAINT fk_integration_erp_recon_batch FOREIGN KEY (batch_id) REFERENCES integration.erp_export_batches(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_erp_recon_line FOREIGN KEY (line_id) REFERENCES integration.erp_export_lines(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_erp_recon_status CHECK (status IN ('ACCEPTED','REJECTED'))
            );
            CREATE INDEX ix_integration_erp_recon_batch_time ON integration.erp_reconciliation_events(batch_id, occurred_at DESC);

            CREATE OR REPLACE FUNCTION integration.prevent_erp_reconciliation_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'ERP_RECONCILIATION_EVENT_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_integration_erp_recon_immutable BEFORE UPDATE OR DELETE ON integration.erp_reconciliation_events FOR EACH ROW EXECUTE FUNCTION integration.prevent_erp_reconciliation_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000129', 'integration.import.view', 'View Import Jobs', 'Integration', 'View Excel import jobs, previews and row errors.', TRUE),
                ('20000000-0000-0000-0000-000000000130', 'integration.import.manage', 'Manage Import Jobs', 'Integration', 'Upload, map, validate and process Excel imports.', TRUE),
                ('20000000-0000-0000-0000-000000000131', 'erp.account.view', 'View ERP Account Mappings', 'ERP', 'View cost category to ERP account mappings.', TRUE),
                ('20000000-0000-0000-0000-000000000132', 'erp.account.manage', 'Manage ERP Account Mappings', 'ERP', 'Create and update cost category ERP account mappings.', TRUE),
                ('20000000-0000-0000-0000-000000000133', 'erp.batch.view', 'View ERP Export Batches', 'ERP', 'View ERP export batches, lines and amount variances.', TRUE),
                ('20000000-0000-0000-0000-000000000134', 'erp.batch.manage', 'Manage ERP Export Batches', 'ERP', 'Create, export, send and close ERP batches.', TRUE),
                ('20000000-0000-0000-0000-000000000135', 'erp.reconciliation.manage', 'Manage ERP Reconciliation', 'ERP', 'Record ERP accepted/rejected results and amount variance.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000129', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000129', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000130', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000130', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000131', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000131', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000132', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000132', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000133', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000133', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000134', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000134', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000135', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000135', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000129','30000000-0000-0000-0000-000000000130','30000000-0000-0000-0000-000000000131','30000000-0000-0000-0000-000000000132',
                '30000000-0000-0000-0000-000000000133','30000000-0000-0000-0000-000000000134','30000000-0000-0000-0000-000000000135');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000129','20000000-0000-0000-0000-000000000130','20000000-0000-0000-0000-000000000131','20000000-0000-0000-0000-000000000132',
                '20000000-0000-0000-0000-000000000133','20000000-0000-0000-0000-000000000134','20000000-0000-0000-0000-000000000135');
            DROP TRIGGER IF EXISTS trg_integration_erp_recon_immutable ON integration.erp_reconciliation_events;
            DROP FUNCTION IF EXISTS integration.prevent_erp_reconciliation_mutation();
            DROP TABLE IF EXISTS integration.erp_reconciliation_events;
            DROP TABLE IF EXISTS integration.erp_export_lines;
            DROP TABLE IF EXISTS integration.erp_export_batches;
            DROP TABLE IF EXISTS integration.erp_account_mappings;
            DROP TRIGGER IF EXISTS trg_integration_import_row_raw_immutable ON integration.import_rows;
            DROP FUNCTION IF EXISTS integration.prevent_import_row_raw_mutation();
            DROP TRIGGER IF EXISTS trg_integration_import_source_immutable ON integration.import_jobs;
            DROP FUNCTION IF EXISTS integration.prevent_import_raw_mutation();
            DROP TABLE IF EXISTS integration.import_rows;
            DROP TABLE IF EXISTS integration.import_jobs;
            """);
    }
}
