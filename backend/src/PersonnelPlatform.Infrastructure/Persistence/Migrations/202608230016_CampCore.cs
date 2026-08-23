using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230016_CampCore")]
public sealed class CampCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS camp;
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE camp.camps (
                id uuid NOT NULL CONSTRAINT pk_camps PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                address varchar(1000) NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_camps_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_camps_company_code ON camp.camps(company_id, code);
            CREATE UNIQUE INDEX ux_camps_company_id ON camp.camps(company_id, id);

            CREATE TABLE camp.rooms (
                id uuid NOT NULL CONSTRAINT pk_camp_rooms PRIMARY KEY,
                camp_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                floor integer NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_camp_rooms_camp FOREIGN KEY (camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_camp_rooms_camp_code ON camp.rooms(camp_id, code);
            CREATE UNIQUE INDEX ux_camp_rooms_camp_id ON camp.rooms(camp_id, id);

            CREATE TABLE camp.beds (
                id uuid NOT NULL CONSTRAINT pk_camp_beds PRIMARY KEY,
                room_id uuid NOT NULL,
                code varchar(80) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_camp_beds_room FOREIGN KEY (room_id) REFERENCES camp.rooms(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_camp_beds_room_code ON camp.beds(room_id, code);
            CREATE UNIQUE INDEX ux_camp_beds_room_id ON camp.beds(room_id, id);

            CREATE TABLE camp.accommodation_rates (
                id uuid NOT NULL CONSTRAINT pk_accommodation_rates PRIMARY KEY,
                camp_id uuid NOT NULL,
                valid_from date NOT NULL,
                valid_until_exclusive date NULL,
                nightly_rate numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_accommodation_rates_camp FOREIGN KEY (camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT,
                CONSTRAINT ck_accommodation_rates_dates CHECK (valid_until_exclusive IS NULL OR valid_until_exclusive > valid_from),
                CONSTRAINT ck_accommodation_rates_amount CHECK (nightly_rate > 0),
                CONSTRAINT ck_accommodation_rates_currency CHECK (currency ~ '^[A-Z]{3}$')
            );
            CREATE INDEX ix_accommodation_rates_camp_from ON camp.accommodation_rates(camp_id, valid_from DESC);
            CREATE UNIQUE INDEX ux_accommodation_rates_camp_id ON camp.accommodation_rates(camp_id, id);
            ALTER TABLE camp.accommodation_rates
                ADD CONSTRAINT ex_accommodation_rates_overlap
                EXCLUDE USING gist (
                    camp_id WITH =,
                    daterange(valid_from, COALESCE(valid_until_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL);

            CREATE TABLE camp.accommodation_stays (
                id uuid NOT NULL CONSTRAINT pk_accommodation_stays PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                camp_id uuid NOT NULL,
                room_id uuid NOT NULL,
                bed_id uuid NOT NULL,
                rate_id uuid NOT NULL,
                project_id_snapshot uuid NULL,
                cost_center_id_snapshot uuid NULL,
                check_in_date date NOT NULL,
                check_out_date_exclusive date NULL,
                nightly_rate_snapshot numeric(18,2) NOT NULL,
                currency_snapshot char(3) NOT NULL,
                total_cost_snapshot numeric(18,2) NOT NULL DEFAULT 0,
                status varchar(20) NOT NULL,
                note varchar(2000) NULL,
                closed_at timestamptz NULL,
                closed_by uuid NULL,
                cancelled_at timestamptz NULL,
                cancelled_by uuid NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_accommodation_stays_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_camp FOREIGN KEY (camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_room FOREIGN KEY (room_id) REFERENCES camp.rooms(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_bed FOREIGN KEY (bed_id) REFERENCES camp.beds(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_rate FOREIGN KEY (rate_id) REFERENCES camp.accommodation_rates(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_project_snapshot FOREIGN KEY (project_id_snapshot) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_cost_center_snapshot FOREIGN KEY (cost_center_id_snapshot) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_company_camp FOREIGN KEY (company_id, camp_id) REFERENCES camp.camps(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_camp_room FOREIGN KEY (camp_id, room_id) REFERENCES camp.rooms(camp_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_room_bed FOREIGN KEY (room_id, bed_id) REFERENCES camp.beds(room_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_accommodation_stays_camp_rate FOREIGN KEY (camp_id, rate_id) REFERENCES camp.accommodation_rates(camp_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_accommodation_stays_dates CHECK (check_out_date_exclusive IS NULL OR check_out_date_exclusive > check_in_date),
                CONSTRAINT ck_accommodation_stays_rate CHECK (nightly_rate_snapshot > 0),
                CONSTRAINT ck_accommodation_stays_currency CHECK (currency_snapshot ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_accommodation_stays_total CHECK (total_cost_snapshot >= 0),
                CONSTRAINT ck_accommodation_stays_status CHECK (status IN ('ACTIVE','CLOSED','CANCELLED')),
                CONSTRAINT ck_accommodation_stays_closed CHECK (status <> 'CLOSED' OR check_out_date_exclusive IS NOT NULL)
            );
            CREATE INDEX ix_accommodation_stays_employee_date ON camp.accommodation_stays(employee_id, check_in_date DESC);
            CREATE INDEX ix_accommodation_stays_camp_status_date ON camp.accommodation_stays(camp_id, status, check_in_date DESC);
            CREATE INDEX ix_accommodation_stays_bed ON camp.accommodation_stays(bed_id);

            ALTER TABLE camp.accommodation_stays
                ADD CONSTRAINT ex_accommodation_stays_bed_overlap
                EXCLUDE USING gist (
                    bed_id WITH =,
                    daterange(check_in_date, COALESCE(check_out_date_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL AND status <> 'CANCELLED');

            ALTER TABLE camp.accommodation_stays
                ADD CONSTRAINT ex_accommodation_stays_employee_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    daterange(check_in_date, COALESCE(check_out_date_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL AND status <> 'CANCELLED');

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000066', 'camp.site.view', 'View Camps', 'Camp', 'View camps, rooms and beds in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000067', 'camp.site.manage', 'Manage Camps', 'Camp', 'Create and maintain camps, rooms and beds.', TRUE),
                ('20000000-0000-0000-0000-000000000068', 'camp.rate.view', 'View Accommodation Rates', 'Camp', 'View date-effective accommodation rates.', TRUE),
                ('20000000-0000-0000-0000-000000000069', 'camp.rate.manage', 'Manage Accommodation Rates', 'Camp', 'Create versioned accommodation rates without overlap.', TRUE),
                ('20000000-0000-0000-0000-000000000070', 'camp.stay.view', 'View Accommodation Stays', 'Camp', 'View employee camp accommodation history and current occupancy.', TRUE),
                ('20000000-0000-0000-0000-000000000071', 'camp.stay.manage', 'Manage Accommodation Stays', 'Camp', 'Assign, close and cancel employee accommodation stays.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000066', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000066', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000067', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000067', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000068', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000068', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000069', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000069', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000070', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000070', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000071', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000071', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000066','30000000-0000-0000-0000-000000000067','30000000-0000-0000-0000-000000000068',
                '30000000-0000-0000-0000-000000000069','30000000-0000-0000-0000-000000000070','30000000-0000-0000-0000-000000000071');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000066','20000000-0000-0000-0000-000000000067','20000000-0000-0000-0000-000000000068',
                '20000000-0000-0000-0000-000000000069','20000000-0000-0000-0000-000000000070','20000000-0000-0000-0000-000000000071');
            DROP TABLE IF EXISTS camp.accommodation_stays;
            DROP TABLE IF EXISTS camp.accommodation_rates;
            DROP TABLE IF EXISTS camp.beds;
            DROP TABLE IF EXISTS camp.rooms;
            DROP TABLE IF EXISTS camp.camps;
            DROP SCHEMA IF EXISTS camp;
            """);
    }
}
