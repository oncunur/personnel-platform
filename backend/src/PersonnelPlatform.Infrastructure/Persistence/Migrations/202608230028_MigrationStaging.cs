using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230028_MigrationStaging")]
public sealed class MigrationStaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS migration;

            CREATE TABLE migration.runs (
                id uuid NOT NULL CONSTRAINT pk_migration_runs PRIMARY KEY,
                company_id uuid NOT NULL,
                source_system varchar(100) NOT NULL,
                source_object varchar(150) NOT NULL,
                target_entity varchar(150) NOT NULL,
                source_file_name varchar(260) NOT NULL,
                source_content_hash char(64) NOT NULL,
                mapping_hash char(64) NOT NULL,
                status varchar(30) NOT NULL,
                total_rows integer NOT NULL DEFAULT 0,
                valid_rows integer NOT NULL DEFAULT 0,
                warning_rows integer NOT NULL DEFAULT 0,
                error_rows integer NOT NULL DEFAULT 0,
                duplicate_rows integer NOT NULL DEFAULT 0,
                reconciliation_mismatch_count integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_migration_run_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_migration_run_status CHECK (status IN ('CREATED','STAGED','VALIDATED','RECONCILED','BLOCKED')),
                CONSTRAINT ck_migration_run_hashes CHECK (source_content_hash ~ '^[0-9A-F]{64}$' AND mapping_hash ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_migration_run_counts CHECK (total_rows >= 0 AND valid_rows >= 0 AND warning_rows >= 0 AND error_rows >= 0 AND duplicate_rows >= 0 AND reconciliation_mismatch_count >= 0)
            );
            CREATE INDEX ix_migration_run_company_time ON migration.runs(company_id, created_at DESC);
            CREATE INDEX ix_migration_run_source_target ON migration.runs(company_id, source_system, source_object, target_entity);
            CREATE INDEX ix_migration_run_content_mapping ON migration.runs(company_id, source_content_hash, mapping_hash);

            CREATE TABLE migration.stage_rows (
                id uuid NOT NULL CONSTRAINT pk_migration_stage_rows PRIMARY KEY,
                migration_run_id uuid NOT NULL,
                row_number integer NOT NULL,
                source_key varchar(240) NOT NULL,
                lineage_key varchar(500) NOT NULL,
                source_row_hash char(64) NOT NULL,
                source_payload_ciphertext text NOT NULL,
                transformed_payload_ciphertext text NOT NULL,
                status varchar(30) NOT NULL,
                idempotence_status varchar(20) NOT NULL,
                warning_code varchar(120) NULL,
                warning_message varchar(2000) NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                previous_run_id uuid NULL,
                previous_stage_row_id uuid NULL,
                staged_at timestamptz NOT NULL,
                CONSTRAINT fk_migration_stage_row_run FOREIGN KEY (migration_run_id) REFERENCES migration.runs(id) ON DELETE RESTRICT,
                CONSTRAINT fk_migration_stage_previous_run FOREIGN KEY (previous_run_id) REFERENCES migration.runs(id) ON DELETE RESTRICT,
                CONSTRAINT fk_migration_stage_previous_row FOREIGN KEY (previous_stage_row_id) REFERENCES migration.stage_rows(id) ON DELETE RESTRICT,
                CONSTRAINT ck_migration_stage_row_number CHECK (row_number > 0),
                CONSTRAINT ck_migration_stage_row_hash CHECK (source_row_hash ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_migration_stage_row_status CHECK (status IN ('VALID','WARNING','ERROR','DUPLICATE')),
                CONSTRAINT ck_migration_stage_idempotence CHECK (idempotence_status IN ('NEW','CHANGED','UNCHANGED')),
                CONSTRAINT ck_migration_stage_payload CHECK (char_length(source_payload_ciphertext) > 0 AND char_length(transformed_payload_ciphertext) > 0),
                CONSTRAINT ck_migration_stage_error CHECK (status <> 'ERROR' OR error_code IS NOT NULL),
                CONSTRAINT ck_migration_stage_warning CHECK (status <> 'WARNING' OR warning_code IS NOT NULL),
                CONSTRAINT ck_migration_stage_duplicate CHECK (status <> 'DUPLICATE' OR idempotence_status = 'UNCHANGED')
            );
            CREATE UNIQUE INDEX ux_migration_stage_row_number ON migration.stage_rows(migration_run_id, row_number);
            CREATE UNIQUE INDEX ux_migration_stage_row_source_key ON migration.stage_rows(migration_run_id, source_key);
            CREATE INDEX ix_migration_stage_row_status ON migration.stage_rows(migration_run_id, status);
            CREATE INDEX ix_migration_stage_row_lineage ON migration.stage_rows(lineage_key);

            CREATE OR REPLACE FUNCTION migration.prevent_stage_row_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'MIGRATION_STAGE_ROW_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_migration_stage_row_immutable BEFORE UPDATE OR DELETE ON migration.stage_rows FOR EACH ROW EXECUTE FUNCTION migration.prevent_stage_row_mutation();

            CREATE TABLE migration.lineage_records (
                id uuid NOT NULL CONSTRAINT pk_migration_lineage_records PRIMARY KEY,
                company_id uuid NOT NULL,
                source_system varchar(100) NOT NULL,
                source_object varchar(150) NOT NULL,
                source_key varchar(240) NOT NULL,
                target_entity varchar(150) NOT NULL,
                last_source_row_hash char(64) NOT NULL,
                last_run_id uuid NOT NULL,
                last_stage_row_id uuid NOT NULL,
                target_entity_id uuid NULL,
                seen_count integer NOT NULL DEFAULT 1,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_migration_lineage_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_migration_lineage_last_run FOREIGN KEY (last_run_id) REFERENCES migration.runs(id) ON DELETE RESTRICT,
                CONSTRAINT fk_migration_lineage_last_stage_row FOREIGN KEY (last_stage_row_id) REFERENCES migration.stage_rows(id) ON DELETE RESTRICT,
                CONSTRAINT ck_migration_lineage_hash CHECK (last_source_row_hash ~ '^[0-9A-F]{64}$'),
                CONSTRAINT ck_migration_lineage_seen CHECK (seen_count > 0)
            );
            CREATE UNIQUE INDEX ux_migration_lineage_source_target ON migration.lineage_records(company_id, source_system, source_object, source_key, target_entity) WHERE deleted_at IS NULL;
            CREATE INDEX ix_migration_lineage_target ON migration.lineage_records(target_entity, target_entity_id);

            CREATE TABLE migration.reconciliations (
                id uuid NOT NULL CONSTRAINT pk_migration_reconciliations PRIMARY KEY,
                migration_run_id uuid NOT NULL,
                metric_code varchar(100) NOT NULL,
                metric_name varchar(200) NOT NULL,
                source_value numeric(20,4) NOT NULL,
                target_value numeric(20,4) NOT NULL,
                difference numeric(20,4) NOT NULL,
                tolerance numeric(20,4) NOT NULL,
                status varchar(20) NOT NULL,
                notes varchar(2000) NULL,
                recorded_at timestamptz NOT NULL,
                recorded_by uuid NOT NULL,
                CONSTRAINT fk_migration_reconciliation_run FOREIGN KEY (migration_run_id) REFERENCES migration.runs(id) ON DELETE RESTRICT,
                CONSTRAINT ck_migration_reconciliation_status CHECK (status IN ('MATCH','MISMATCH')),
                CONSTRAINT ck_migration_reconciliation_values CHECK (difference >= 0 AND tolerance >= 0)
            );
            CREATE UNIQUE INDEX ux_migration_reconciliation_metric ON migration.reconciliations(migration_run_id, metric_code);
            CREATE INDEX ix_migration_reconciliation_status ON migration.reconciliations(migration_run_id, status);

            CREATE OR REPLACE FUNCTION migration.prevent_reconciliation_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'MIGRATION_RECONCILIATION_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_migration_reconciliation_immutable BEFORE UPDATE OR DELETE ON migration.reconciliations FOR EACH ROW EXECUTE FUNCTION migration.prevent_reconciliation_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000140', 'migration.view', 'View Migration Runs', 'Migration', 'View migration runs, staged row outcomes and reconciliation evidence.', TRUE),
                ('20000000-0000-0000-0000-000000000141', 'migration.manage', 'Manage Migration Runs', 'Migration', 'Create, stage and validate controlled legacy-data migration runs.', TRUE),
                ('20000000-0000-0000-0000-000000000142', 'migration.reconcile', 'Reconcile Migration Runs', 'Migration', 'Record source-to-target migration reconciliation metrics.', TRUE)
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000140', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000140', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000141', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000141', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000142', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000142', '2026-08-23T00:00:00Z')
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000140','30000000-0000-0000-0000-000000000141','30000000-0000-0000-0000-000000000142');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000140','20000000-0000-0000-0000-000000000141','20000000-0000-0000-0000-000000000142');
            DROP TRIGGER IF EXISTS trg_migration_reconciliation_immutable ON migration.reconciliations;
            DROP FUNCTION IF EXISTS migration.prevent_reconciliation_mutation();
            DROP TABLE IF EXISTS migration.reconciliations;
            DROP TABLE IF EXISTS migration.lineage_records;
            DROP TRIGGER IF EXISTS trg_migration_stage_row_immutable ON migration.stage_rows;
            DROP FUNCTION IF EXISTS migration.prevent_stage_row_mutation();
            DROP TABLE IF EXISTS migration.stage_rows;
            DROP TABLE IF EXISTS migration.runs;
            DROP SCHEMA IF EXISTS migration;
            """);
    }
}