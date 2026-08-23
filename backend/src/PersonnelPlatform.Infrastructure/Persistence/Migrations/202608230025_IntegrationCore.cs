using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230025_IntegrationCore")]
public sealed class IntegrationCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS integration;

            CREATE TABLE integration.systems (
                id uuid NOT NULL CONSTRAINT pk_integration_systems PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                system_type varchar(30) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_system_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_system_type CHECK (system_type IN ('PDKS','MEAL','ERP','IMPORT'))
            );
            CREATE UNIQUE INDEX ux_integration_system_company_code ON integration.systems(company_id, code) WHERE deleted_at IS NULL;
            CREATE INDEX ix_integration_system_company_active ON integration.systems(company_id, is_active);

            CREATE TABLE integration.devices (
                id uuid NOT NULL CONSTRAINT pk_integration_devices PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                code varchar(100) NOT NULL,
                name varchar(150) NOT NULL,
                device_type varchar(40) NOT NULL,
                scoped_camp_id uuid NULL,
                credential_hash varchar(128) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                last_seen_at timestamptz NULL,
                last_error_at timestamptz NULL,
                last_error_message varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_device_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_device_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_device_camp FOREIGN KEY (scoped_camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_device_type CHECK (device_type IN ('PDKS_TERMINAL','MEAL_TERMINAL','GENERIC')),
                CONSTRAINT ck_integration_device_credential CHECK (char_length(credential_hash) >= 64)
            );
            CREATE UNIQUE INDEX ux_integration_device_system_code ON integration.devices(integration_system_id, code) WHERE deleted_at IS NULL;
            CREATE INDEX ix_integration_device_company_active ON integration.devices(company_id, is_active);

            CREATE TABLE integration.entity_mappings (
                id uuid NOT NULL CONSTRAINT pk_integration_entity_mappings PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                entity_type varchar(40) NOT NULL,
                external_code varchar(200) NOT NULL,
                internal_entity_id uuid NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_mapping_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_mapping_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_mapping_type CHECK (entity_type IN ('EMPLOYEE','CAMP','MEAL_TYPE','PROJECT','COST_CENTER','COST_CATEGORY'))
            );
            CREATE UNIQUE INDEX ux_integration_mapping_external ON integration.entity_mappings(integration_system_id, entity_type, external_code) WHERE deleted_at IS NULL;
            CREATE INDEX ix_integration_mapping_internal ON integration.entity_mappings(company_id, entity_type, internal_entity_id);

            CREATE TABLE integration.staging_records (
                id uuid NOT NULL CONSTRAINT pk_integration_staging_records PRIMARY KEY,
                company_id uuid NOT NULL,
                integration_system_id uuid NOT NULL,
                device_id uuid NULL,
                event_type varchar(50) NOT NULL,
                external_event_id varchar(240) NOT NULL,
                payload_json jsonb NOT NULL,
                status varchar(30) NOT NULL,
                attempt_count integer NOT NULL DEFAULT 0,
                next_retry_at timestamptz NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                processed_entity_type varchar(60) NULL,
                processed_entity_id uuid NULL,
                received_at timestamptz NOT NULL,
                last_attempt_at timestamptz NULL,
                processed_at timestamptz NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_integration_staging_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_staging_system FOREIGN KEY (integration_system_id) REFERENCES integration.systems(id) ON DELETE RESTRICT,
                CONSTRAINT fk_integration_staging_device FOREIGN KEY (device_id) REFERENCES integration.devices(id) ON DELETE RESTRICT,
                CONSTRAINT ck_integration_staging_event CHECK (event_type IN ('ATTENDANCE_EVENT','MEAL_CONSUMPTION','IMPORT_ROW')),
                CONSTRAINT ck_integration_staging_status CHECK (status IN ('RECEIVED','PROCESSING','PROCESSED','BUSINESS_ERROR','TECHNICAL_ERROR','DEAD_LETTER')),
                CONSTRAINT ck_integration_staging_attempt CHECK (attempt_count >= 0),
                CONSTRAINT ck_integration_staging_payload CHECK (jsonb_typeof(payload_json) = 'object'),
                CONSTRAINT ck_integration_staging_processed CHECK (status <> 'PROCESSED' OR (processed_entity_type IS NOT NULL AND processed_entity_id IS NOT NULL AND processed_at IS NOT NULL))
            );
            CREATE UNIQUE INDEX ux_integration_staging_external ON integration.staging_records(integration_system_id, event_type, external_event_id);
            CREATE INDEX ix_integration_staging_work_queue ON integration.staging_records(status, next_retry_at, received_at);
            CREATE INDEX ix_integration_staging_company_time ON integration.staging_records(company_id, received_at DESC);
            CREATE INDEX ix_integration_staging_system_time ON integration.staging_records(integration_system_id, received_at DESC);

            CREATE OR REPLACE FUNCTION integration.prevent_raw_staging_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id IS DISTINCT FROM OLD.company_id
                   OR NEW.integration_system_id IS DISTINCT FROM OLD.integration_system_id
                   OR NEW.device_id IS DISTINCT FROM OLD.device_id
                   OR NEW.event_type IS DISTINCT FROM OLD.event_type
                   OR NEW.external_event_id IS DISTINCT FROM OLD.external_event_id
                   OR NEW.payload_json IS DISTINCT FROM OLD.payload_json
                   OR NEW.received_at IS DISTINCT FROM OLD.received_at
                   OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
                    RAISE EXCEPTION 'INTEGRATION_STAGING_RAW_IMMUTABLE' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_integration_staging_raw_immutable BEFORE UPDATE ON integration.staging_records FOR EACH ROW EXECUTE FUNCTION integration.prevent_raw_staging_mutation();

            CREATE TABLE integration.staging_history (
                id uuid NOT NULL CONSTRAINT pk_integration_staging_history PRIMARY KEY,
                staging_record_id uuid NOT NULL,
                event_type varchar(50) NOT NULL,
                from_status varchar(30) NOT NULL,
                to_status varchar(30) NOT NULL,
                error_code varchar(120) NULL,
                error_message varchar(2000) NULL,
                actor_user_id uuid NULL,
                occurred_at timestamptz NOT NULL,
                CONSTRAINT fk_integration_history_staging FOREIGN KEY (staging_record_id) REFERENCES integration.staging_records(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_integration_history_staging_time ON integration.staging_history(staging_record_id, occurred_at DESC);

            CREATE OR REPLACE FUNCTION integration.prevent_history_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'INTEGRATION_STAGING_HISTORY_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_integration_history_immutable BEFORE UPDATE OR DELETE ON integration.staging_history FOR EACH ROW EXECUTE FUNCTION integration.prevent_history_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000122', 'integration.system.view', 'View Integration Systems', 'Integration', 'View external systems and devices.', TRUE),
                ('20000000-0000-0000-0000-000000000123', 'integration.system.manage', 'Manage Integration Systems', 'Integration', 'Create and manage systems, devices and device credentials.', TRUE),
                ('20000000-0000-0000-0000-000000000124', 'integration.mapping.view', 'View Integration Mappings', 'Integration', 'View external entity mappings.', TRUE),
                ('20000000-0000-0000-0000-000000000125', 'integration.mapping.manage', 'Manage Integration Mappings', 'Integration', 'Create and update external entity mappings.', TRUE),
                ('20000000-0000-0000-0000-000000000126', 'integration.queue.view', 'View Integration Queue', 'Integration', 'View raw staging, errors and processing history.', TRUE),
                ('20000000-0000-0000-0000-000000000127', 'integration.queue.reprocess', 'Reprocess Integration Queue', 'Integration', 'Requeue failed integration records and run scoped processing.', TRUE),
                ('20000000-0000-0000-0000-000000000128', 'integration.monitor.view', 'View Integration Monitoring', 'Integration', 'View device health, last events and queue backlog.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000122', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000122', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000123', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000123', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000124', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000124', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000125', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000125', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000126', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000126', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000127', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000127', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000128', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000128', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000122','30000000-0000-0000-0000-000000000123','30000000-0000-0000-0000-000000000124','30000000-0000-0000-0000-000000000125',
                '30000000-0000-0000-0000-000000000126','30000000-0000-0000-0000-000000000127','30000000-0000-0000-0000-000000000128');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000122','20000000-0000-0000-0000-000000000123','20000000-0000-0000-0000-000000000124','20000000-0000-0000-0000-000000000125',
                '20000000-0000-0000-0000-000000000126','20000000-0000-0000-0000-000000000127','20000000-0000-0000-0000-000000000128');
            DROP TRIGGER IF EXISTS trg_integration_history_immutable ON integration.staging_history;
            DROP FUNCTION IF EXISTS integration.prevent_history_mutation();
            DROP TABLE IF EXISTS integration.staging_history;
            DROP TRIGGER IF EXISTS trg_integration_staging_raw_immutable ON integration.staging_records;
            DROP FUNCTION IF EXISTS integration.prevent_raw_staging_mutation();
            DROP TABLE IF EXISTS integration.staging_records;
            DROP TABLE IF EXISTS integration.entity_mappings;
            DROP TABLE IF EXISTS integration.devices;
            DROP TABLE IF EXISTS integration.systems;
            DROP SCHEMA IF EXISTS integration;
            """);
    }
}
