using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230019_AssetStockCore")]
public sealed class AssetStockCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS administration;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_employees_company_id ON hr.employees(company_id, id);

            CREATE TABLE administration.stock_locations (
                id uuid NOT NULL CONSTRAINT pk_stock_locations PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_stock_locations_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_stock_locations_company_code ON administration.stock_locations(company_id, code);
            CREATE UNIQUE INDEX ux_stock_locations_company_id ON administration.stock_locations(company_id, id);

            CREATE TABLE administration.stock_items (
                id uuid NOT NULL CONSTRAINT pk_stock_items PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                unit varchar(20) NOT NULL,
                minimum_level numeric(18,3) NOT NULL DEFAULT 0,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_stock_items_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_stock_items_minimum CHECK (minimum_level >= 0)
            );
            CREATE UNIQUE INDEX ux_stock_items_company_code ON administration.stock_items(company_id, code);
            CREATE UNIQUE INDEX ux_stock_items_company_id ON administration.stock_items(company_id, id);

            CREATE TABLE administration.stock_movements (
                id uuid NOT NULL CONSTRAINT pk_stock_movements PRIMARY KEY,
                company_id uuid NOT NULL,
                stock_item_id uuid NOT NULL,
                location_id uuid NOT NULL,
                employee_id uuid NULL,
                project_id_snapshot uuid NULL,
                cost_center_id_snapshot uuid NULL,
                movement_type varchar(30) NOT NULL,
                quantity numeric(18,3) NOT NULL,
                source varchar(20) NOT NULL,
                external_event_id varchar(200) NULL,
                note varchar(1000) NULL,
                occurred_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_stock_movements_item FOREIGN KEY (stock_item_id) REFERENCES administration.stock_items(id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_location FOREIGN KEY (location_id) REFERENCES administration.stock_locations(id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_project_snapshot FOREIGN KEY (project_id_snapshot) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_cost_center_snapshot FOREIGN KEY (cost_center_id_snapshot) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_company_item FOREIGN KEY (company_id, stock_item_id) REFERENCES administration.stock_items(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_company_location FOREIGN KEY (company_id, location_id) REFERENCES administration.stock_locations(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_stock_movements_company_employee FOREIGN KEY (company_id, employee_id) REFERENCES hr.employees(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_stock_movements_type CHECK (movement_type IN ('RECEIPT','ISSUE','RETURN','CORRECTION_IN','CORRECTION_OUT')),
                CONSTRAINT ck_stock_movements_quantity CHECK (quantity > 0),
                CONSTRAINT ck_stock_movements_source CHECK (source IN ('MANUAL','IMPORT','INTEGRATION')),
                CONSTRAINT ck_stock_movements_external CHECK (source = 'MANUAL' OR external_event_id IS NOT NULL)
            );
            CREATE INDEX ix_stock_movements_item_location_time ON administration.stock_movements(stock_item_id, location_id, occurred_at DESC);
            CREATE INDEX ix_stock_movements_company_time ON administration.stock_movements(company_id, occurred_at DESC);
            CREATE UNIQUE INDEX ux_stock_movements_company_source_external ON administration.stock_movements(company_id, source, external_event_id) WHERE external_event_id IS NOT NULL;

            CREATE OR REPLACE FUNCTION administration.guard_stock_movement_insert()
            RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE current_balance numeric(18,3);
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtextextended(NEW.stock_item_id::text || ':' || NEW.location_id::text, 0));
                IF NEW.movement_type IN ('ISSUE','CORRECTION_OUT') THEN
                    SELECT COALESCE(SUM(CASE WHEN movement_type IN ('RECEIPT','RETURN','CORRECTION_IN') THEN quantity ELSE -quantity END), 0)
                      INTO current_balance
                      FROM administration.stock_movements
                     WHERE stock_item_id = NEW.stock_item_id AND location_id = NEW.location_id;
                    IF current_balance < NEW.quantity THEN
                        RAISE EXCEPTION 'STOCK_NEGATIVE_NOT_ALLOWED' USING ERRCODE = 'P0001';
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_stock_movement_balance_guard
            BEFORE INSERT ON administration.stock_movements
            FOR EACH ROW EXECUTE FUNCTION administration.guard_stock_movement_insert();

            CREATE OR REPLACE FUNCTION administration.prevent_stock_movement_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'STOCK_MOVEMENT_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_stock_movements_immutable
            BEFORE UPDATE OR DELETE ON administration.stock_movements
            FOR EACH ROW EXECUTE FUNCTION administration.prevent_stock_movement_mutation();

            CREATE TABLE administration.assets (
                id uuid NOT NULL CONSTRAINT pk_assets PRIMARY KEY,
                company_id uuid NOT NULL,
                location_id uuid NULL,
                asset_tag varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                category varchar(100) NOT NULL,
                serial_number varchar(150) NULL,
                status varchar(30) NOT NULL,
                purchase_date date NULL,
                purchase_cost numeric(18,2) NULL,
                currency char(3) NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_assets_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_assets_location FOREIGN KEY (location_id) REFERENCES administration.stock_locations(id) ON DELETE RESTRICT,
                CONSTRAINT fk_assets_company_location FOREIGN KEY (company_id, location_id) REFERENCES administration.stock_locations(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_assets_status CHECK (status IN ('AVAILABLE','ASSIGNED','MAINTENANCE','LOST','RETIRED')),
                CONSTRAINT ck_assets_cost CHECK (purchase_cost IS NULL OR purchase_cost >= 0),
                CONSTRAINT ck_assets_currency CHECK ((purchase_cost IS NULL OR purchase_cost = 0) OR currency ~ '^[A-Z]{3}$')
            );
            CREATE UNIQUE INDEX ux_assets_company_tag ON administration.assets(company_id, asset_tag);
            CREATE UNIQUE INDEX ux_assets_company_serial ON administration.assets(company_id, serial_number) WHERE serial_number IS NOT NULL;
            CREATE UNIQUE INDEX ux_assets_company_id ON administration.assets(company_id, id);
            CREATE INDEX ix_assets_company_status ON administration.assets(company_id, status);

            CREATE TABLE administration.asset_assignments (
                id uuid NOT NULL CONSTRAINT pk_asset_assignments PRIMARY KEY,
                company_id uuid NOT NULL,
                asset_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                project_id_snapshot uuid NULL,
                cost_center_id_snapshot uuid NULL,
                assigned_date date NOT NULL,
                due_date date NULL,
                returned_date date NULL,
                status varchar(30) NOT NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_asset_assignments_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_asset FOREIGN KEY (asset_id) REFERENCES administration.assets(id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_project_snapshot FOREIGN KEY (project_id_snapshot) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_cost_center_snapshot FOREIGN KEY (cost_center_id_snapshot) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_company_asset FOREIGN KEY (company_id, asset_id) REFERENCES administration.assets(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_asset_assignments_company_employee FOREIGN KEY (company_id, employee_id) REFERENCES hr.employees(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_asset_assignments_status CHECK (status IN ('ACTIVE','RETURNED','DAMAGED','LOST')),
                CONSTRAINT ck_asset_assignments_due CHECK (due_date IS NULL OR due_date >= assigned_date),
                CONSTRAINT ck_asset_assignments_return CHECK (returned_date IS NULL OR returned_date >= assigned_date)
            );
            CREATE UNIQUE INDEX ux_asset_assignments_active_asset ON administration.asset_assignments(asset_id) WHERE status = 'ACTIVE' AND deleted_at IS NULL;
            CREATE INDEX ix_asset_assignments_asset_status ON administration.asset_assignments(asset_id, status);
            CREATE INDEX ix_asset_assignments_employee_date ON administration.asset_assignments(employee_id, assigned_date DESC);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000085', 'administration.stock.view', 'View Stock', 'Administration', 'View stock items, locations, balances and movement history.', TRUE),
                ('20000000-0000-0000-0000-000000000086', 'administration.stock.manage', 'Manage Stock Master', 'Administration', 'Create stock locations and stock item master data.', TRUE),
                ('20000000-0000-0000-0000-000000000087', 'administration.stock.movement.record', 'Record Stock Movement', 'Administration', 'Record append-only stock receipts, issues, returns and corrections.', TRUE),
                ('20000000-0000-0000-0000-000000000088', 'administration.asset.view', 'View Assets', 'Administration', 'View asset cards and assignment history.', TRUE),
                ('20000000-0000-0000-0000-000000000089', 'administration.asset.manage', 'Manage Assets', 'Administration', 'Create and maintain asset cards.', TRUE),
                ('20000000-0000-0000-0000-000000000090', 'administration.asset.assign', 'Assign Assets', 'Administration', 'Assign, return, mark damaged or lost personnel assets.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000085', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000085', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000086', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000086', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000087', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000087', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000088', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000088', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000089', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000089', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000090', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000090', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN ('30000000-0000-0000-0000-000000000085','30000000-0000-0000-0000-000000000086','30000000-0000-0000-0000-000000000087','30000000-0000-0000-0000-000000000088','30000000-0000-0000-0000-000000000089','30000000-0000-0000-0000-000000000090');
            DELETE FROM system.permissions WHERE id IN ('20000000-0000-0000-0000-000000000085','20000000-0000-0000-0000-000000000086','20000000-0000-0000-0000-000000000087','20000000-0000-0000-0000-000000000088','20000000-0000-0000-0000-000000000089','20000000-0000-0000-0000-000000000090');
            DROP TABLE IF EXISTS administration.asset_assignments;
            DROP TABLE IF EXISTS administration.assets;
            DROP TRIGGER IF EXISTS trg_stock_movements_immutable ON administration.stock_movements;
            DROP FUNCTION IF EXISTS administration.prevent_stock_movement_mutation();
            DROP TRIGGER IF EXISTS trg_stock_movement_balance_guard ON administration.stock_movements;
            DROP FUNCTION IF EXISTS administration.guard_stock_movement_insert();
            DROP TABLE IF EXISTS administration.stock_movements;
            DROP TABLE IF EXISTS administration.stock_items;
            DROP TABLE IF EXISTS administration.stock_locations;
            DROP INDEX IF EXISTS hr.ux_employees_company_id;
            DROP SCHEMA IF EXISTS administration;
            """);
    }
}
