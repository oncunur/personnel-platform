using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230020_VehicleCore")]
public sealed class VehicleCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE administration.vehicles (
                id uuid NOT NULL CONSTRAINT pk_vehicles PRIMARY KEY,
                company_id uuid NOT NULL,
                plate varchar(30) NOT NULL,
                vin varchar(50) NULL,
                brand varchar(100) NOT NULL,
                model varchar(100) NOT NULL,
                model_year integer NULL,
                status varchar(30) NOT NULL,
                insurance_valid_until date NULL,
                inspection_valid_until date NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_vehicles_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_vehicles_status CHECK (status IN ('ACTIVE','MAINTENANCE','OUT_OF_SERVICE','RETIRED')),
                CONSTRAINT ck_vehicles_model_year CHECK (model_year IS NULL OR model_year BETWEEN 1900 AND 2200)
            );
            CREATE UNIQUE INDEX ux_vehicles_company_plate ON administration.vehicles(company_id, plate) WHERE deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_vehicles_company_vin ON administration.vehicles(company_id, vin) WHERE vin IS NOT NULL AND deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_vehicles_company_id ON administration.vehicles(company_id, id);
            CREATE INDEX ix_vehicles_company_status ON administration.vehicles(company_id, status);

            CREATE TABLE administration.vehicle_assignments (
                id uuid NOT NULL CONSTRAINT pk_vehicle_assignments PRIMARY KEY,
                company_id uuid NOT NULL,
                vehicle_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                project_id_snapshot uuid NULL,
                cost_center_id_snapshot uuid NULL,
                valid_from date NOT NULL,
                valid_until_exclusive date NULL,
                status varchar(30) NOT NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_vehicle_assignments_vehicle FOREIGN KEY (vehicle_id) REFERENCES administration.vehicles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_assignments_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_assignments_project_snapshot FOREIGN KEY (project_id_snapshot) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_assignments_cost_center_snapshot FOREIGN KEY (cost_center_id_snapshot) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_assignments_company_vehicle FOREIGN KEY (company_id, vehicle_id) REFERENCES administration.vehicles(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_assignments_company_employee FOREIGN KEY (company_id, employee_id) REFERENCES hr.employees(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_vehicle_assignments_dates CHECK (valid_until_exclusive IS NULL OR valid_until_exclusive > valid_from),
                CONSTRAINT ck_vehicle_assignments_status CHECK (status IN ('ACTIVE','CLOSED'))
            );
            CREATE INDEX ix_vehicle_assignments_vehicle_date ON administration.vehicle_assignments(vehicle_id, valid_from DESC);
            CREATE INDEX ix_vehicle_assignments_employee_date ON administration.vehicle_assignments(employee_id, valid_from DESC);
            ALTER TABLE administration.vehicle_assignments
                ADD CONSTRAINT ex_vehicle_assignments_overlap
                EXCLUDE USING gist (
                    vehicle_id WITH =,
                    daterange(valid_from, COALESCE(valid_until_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL);

            CREATE TABLE administration.vehicle_odometer_events (
                id uuid NOT NULL CONSTRAINT pk_vehicle_odometer_events PRIMARY KEY,
                company_id uuid NOT NULL,
                vehicle_id uuid NOT NULL,
                odometer_km integer NOT NULL,
                occurred_at timestamptz NOT NULL,
                source varchar(20) NOT NULL,
                external_event_id varchar(200) NULL,
                note varchar(1000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_vehicle_odometer_vehicle FOREIGN KEY (vehicle_id) REFERENCES administration.vehicles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_odometer_company_vehicle FOREIGN KEY (company_id, vehicle_id) REFERENCES administration.vehicles(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_vehicle_odometer_value CHECK (odometer_km BETWEEN 0 AND 10000000),
                CONSTRAINT ck_vehicle_odometer_source CHECK (source IN ('MANUAL','IMPORT','INTEGRATION')),
                CONSTRAINT ck_vehicle_odometer_external CHECK (source = 'MANUAL' OR external_event_id IS NOT NULL)
            );
            CREATE INDEX ix_vehicle_odometer_vehicle_time ON administration.vehicle_odometer_events(vehicle_id, occurred_at DESC);
            CREATE UNIQUE INDEX ux_vehicle_odometer_vehicle_id ON administration.vehicle_odometer_events(vehicle_id, id);
            CREATE UNIQUE INDEX ux_vehicle_odometer_company_source_external ON administration.vehicle_odometer_events(company_id, source, external_event_id) WHERE external_event_id IS NOT NULL;

            CREATE OR REPLACE FUNCTION administration.guard_vehicle_odometer_insert()
            RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE latest_km integer;
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtextextended('vehicle-odometer:' || NEW.vehicle_id::text, 0));
                SELECT MAX(odometer_km) INTO latest_km FROM administration.vehicle_odometer_events WHERE vehicle_id = NEW.vehicle_id;
                IF latest_km IS NOT NULL AND NEW.odometer_km < latest_km THEN
                    RAISE EXCEPTION 'VEHICLE_ODOMETER_REGRESSION' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_vehicle_odometer_monotonic
            BEFORE INSERT ON administration.vehicle_odometer_events
            FOR EACH ROW EXECUTE FUNCTION administration.guard_vehicle_odometer_insert();

            CREATE OR REPLACE FUNCTION administration.prevent_vehicle_ledger_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'VEHICLE_LEDGER_IMMUTABLE' USING ERRCODE = 'P0001';
            END;
            $$;
            CREATE TRIGGER trg_vehicle_odometer_immutable BEFORE UPDATE OR DELETE ON administration.vehicle_odometer_events FOR EACH ROW EXECUTE FUNCTION administration.prevent_vehicle_ledger_mutation();

            CREATE TABLE administration.vehicle_maintenance (
                id uuid NOT NULL CONSTRAINT pk_vehicle_maintenance PRIMARY KEY,
                company_id uuid NOT NULL,
                vehicle_id uuid NOT NULL,
                odometer_event_id uuid NOT NULL,
                maintenance_type varchar(80) NOT NULL,
                description varchar(1000) NOT NULL,
                cost numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                service_date date NOT NULL,
                next_due_date date NULL,
                next_due_odometer_km integer NULL,
                vendor varchar(200) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_vehicle_maintenance_vehicle FOREIGN KEY (vehicle_id) REFERENCES administration.vehicles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_maintenance_odometer FOREIGN KEY (odometer_event_id) REFERENCES administration.vehicle_odometer_events(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_maintenance_company_vehicle FOREIGN KEY (company_id, vehicle_id) REFERENCES administration.vehicles(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_maintenance_odometer_match FOREIGN KEY (vehicle_id, odometer_event_id) REFERENCES administration.vehicle_odometer_events(vehicle_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_vehicle_maintenance_cost CHECK (cost >= 0),
                CONSTRAINT ck_vehicle_maintenance_currency CHECK (currency ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_vehicle_maintenance_next_date CHECK (next_due_date IS NULL OR next_due_date >= service_date),
                CONSTRAINT ck_vehicle_maintenance_next_km CHECK (next_due_odometer_km IS NULL OR next_due_odometer_km >= 0)
            );
            CREATE INDEX ix_vehicle_maintenance_vehicle_date ON administration.vehicle_maintenance(vehicle_id, service_date DESC);
            CREATE TRIGGER trg_vehicle_maintenance_immutable BEFORE UPDATE OR DELETE ON administration.vehicle_maintenance FOR EACH ROW EXECUTE FUNCTION administration.prevent_vehicle_ledger_mutation();

            CREATE TABLE administration.vehicle_fuel (
                id uuid NOT NULL CONSTRAINT pk_vehicle_fuel PRIMARY KEY,
                company_id uuid NOT NULL,
                vehicle_id uuid NOT NULL,
                odometer_event_id uuid NOT NULL,
                liters numeric(18,3) NOT NULL,
                total_cost numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                fueled_at timestamptz NOT NULL,
                station varchar(200) NULL,
                source varchar(20) NOT NULL,
                external_event_id varchar(200) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_vehicle_fuel_vehicle FOREIGN KEY (vehicle_id) REFERENCES administration.vehicles(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_fuel_odometer FOREIGN KEY (odometer_event_id) REFERENCES administration.vehicle_odometer_events(id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_fuel_company_vehicle FOREIGN KEY (company_id, vehicle_id) REFERENCES administration.vehicles(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_vehicle_fuel_odometer_match FOREIGN KEY (vehicle_id, odometer_event_id) REFERENCES administration.vehicle_odometer_events(vehicle_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_vehicle_fuel_liters CHECK (liters > 0 AND liters <= 10000),
                CONSTRAINT ck_vehicle_fuel_cost CHECK (total_cost >= 0),
                CONSTRAINT ck_vehicle_fuel_currency CHECK (currency ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_vehicle_fuel_source CHECK (source IN ('MANUAL','IMPORT','INTEGRATION')),
                CONSTRAINT ck_vehicle_fuel_external CHECK (source = 'MANUAL' OR external_event_id IS NOT NULL)
            );
            CREATE INDEX ix_vehicle_fuel_vehicle_time ON administration.vehicle_fuel(vehicle_id, fueled_at DESC);
            CREATE UNIQUE INDEX ux_vehicle_fuel_company_source_external ON administration.vehicle_fuel(company_id, source, external_event_id) WHERE external_event_id IS NOT NULL;
            CREATE TRIGGER trg_vehicle_fuel_immutable BEFORE UPDATE OR DELETE ON administration.vehicle_fuel FOR EACH ROW EXECUTE FUNCTION administration.prevent_vehicle_ledger_mutation();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000091', 'administration.vehicle.view', 'View Vehicles', 'Administration', 'View vehicles, assignments, odometer, maintenance and fuel history.', TRUE),
                ('20000000-0000-0000-0000-000000000092', 'administration.vehicle.manage', 'Manage Vehicles', 'Administration', 'Create vehicle cards and manage vehicle status.', TRUE),
                ('20000000-0000-0000-0000-000000000093', 'administration.vehicle.assign', 'Assign Vehicles', 'Administration', 'Assign vehicles to personnel and close assignments.', TRUE),
                ('20000000-0000-0000-0000-000000000094', 'administration.vehicle.odometer.record', 'Record Vehicle Odometer', 'Administration', 'Record append-only vehicle odometer events.', TRUE),
                ('20000000-0000-0000-0000-000000000095', 'administration.vehicle.maintenance.manage', 'Manage Vehicle Maintenance', 'Administration', 'Record immutable vehicle maintenance and service costs.', TRUE),
                ('20000000-0000-0000-0000-000000000096', 'administration.vehicle.fuel.record', 'Record Vehicle Fuel', 'Administration', 'Record immutable fuel consumption and costs.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000091', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000091', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000092', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000092', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000093', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000093', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000094', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000094', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000095', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000095', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000096', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000096', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN ('30000000-0000-0000-0000-000000000091','30000000-0000-0000-0000-000000000092','30000000-0000-0000-0000-000000000093','30000000-0000-0000-0000-000000000094','30000000-0000-0000-0000-000000000095','30000000-0000-0000-0000-000000000096');
            DELETE FROM system.permissions WHERE id IN ('20000000-0000-0000-0000-000000000091','20000000-0000-0000-0000-000000000092','20000000-0000-0000-0000-000000000093','20000000-0000-0000-0000-000000000094','20000000-0000-0000-0000-000000000095','20000000-0000-0000-0000-000000000096');
            DROP TABLE IF EXISTS administration.vehicle_fuel;
            DROP TABLE IF EXISTS administration.vehicle_maintenance;
            DROP TRIGGER IF EXISTS trg_vehicle_odometer_immutable ON administration.vehicle_odometer_events;
            DROP TRIGGER IF EXISTS trg_vehicle_odometer_monotonic ON administration.vehicle_odometer_events;
            DROP FUNCTION IF EXISTS administration.guard_vehicle_odometer_insert();
            DROP FUNCTION IF EXISTS administration.prevent_vehicle_ledger_mutation();
            DROP TABLE IF EXISTS administration.vehicle_odometer_events;
            DROP TABLE IF EXISTS administration.vehicle_assignments;
            DROP TABLE IF EXISTS administration.vehicles;
            """);
    }
}
