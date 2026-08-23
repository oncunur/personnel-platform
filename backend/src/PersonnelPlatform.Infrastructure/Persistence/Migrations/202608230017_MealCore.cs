using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230017_MealCore")]
public sealed class MealCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE SCHEMA IF NOT EXISTS meal;
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE meal.meal_types (
                id uuid NOT NULL CONSTRAINT pk_meal_types PRIMARY KEY,
                code varchar(40) NOT NULL,
                name varchar(100) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                display_order integer NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX ux_meal_types_code ON meal.meal_types(code);

            INSERT INTO meal.meal_types (id, code, name, is_active, display_order) VALUES
                ('50000000-0000-0000-0000-000000000001', 'BREAKFAST', 'Kahvaltı', TRUE, 10),
                ('50000000-0000-0000-0000-000000000002', 'LUNCH', 'Öğle Yemeği', TRUE, 20),
                ('50000000-0000-0000-0000-000000000003', 'DINNER', 'Akşam Yemeği', TRUE, 30),
                ('50000000-0000-0000-0000-000000000004', 'SNACK', 'Ara Öğün', TRUE, 40);

            CREATE TABLE meal.meal_rates (
                id uuid NOT NULL CONSTRAINT pk_meal_rates PRIMARY KEY,
                camp_id uuid NOT NULL,
                meal_type_id uuid NOT NULL,
                valid_from date NOT NULL,
                valid_until_exclusive date NULL,
                unit_price numeric(18,2) NOT NULL,
                currency char(3) NOT NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_meal_rates_camp FOREIGN KEY (camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_rates_type FOREIGN KEY (meal_type_id) REFERENCES meal.meal_types(id) ON DELETE RESTRICT,
                CONSTRAINT ck_meal_rates_dates CHECK (valid_until_exclusive IS NULL OR valid_until_exclusive > valid_from),
                CONSTRAINT ck_meal_rates_price CHECK (unit_price > 0),
                CONSTRAINT ck_meal_rates_currency CHECK (currency ~ '^[A-Z]{3}$')
            );
            CREATE INDEX ix_meal_rates_camp_type_from ON meal.meal_rates(camp_id, meal_type_id, valid_from DESC);
            CREATE UNIQUE INDEX ux_meal_rates_camp_type_id ON meal.meal_rates(camp_id, meal_type_id, id);
            ALTER TABLE meal.meal_rates
                ADD CONSTRAINT ex_meal_rates_overlap
                EXCLUDE USING gist (
                    camp_id WITH =,
                    meal_type_id WITH =,
                    daterange(valid_from, COALESCE(valid_until_exclusive, 'infinity'::date), '[)') WITH &&
                ) WHERE (deleted_at IS NULL);

            CREATE TABLE meal.meal_consumptions (
                id uuid NOT NULL CONSTRAINT pk_meal_consumptions PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                camp_id uuid NOT NULL,
                meal_type_id uuid NOT NULL,
                meal_rate_id uuid NOT NULL,
                consumption_date date NOT NULL,
                quantity numeric(10,2) NOT NULL,
                unit_price_snapshot numeric(18,2) NOT NULL,
                currency_snapshot char(3) NOT NULL,
                total_cost_snapshot numeric(18,2) NOT NULL,
                project_id_snapshot uuid NULL,
                cost_center_id_snapshot uuid NULL,
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
                CONSTRAINT fk_meal_consumptions_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_camp FOREIGN KEY (camp_id) REFERENCES camp.camps(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_type FOREIGN KEY (meal_type_id) REFERENCES meal.meal_types(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_rate FOREIGN KEY (meal_rate_id) REFERENCES meal.meal_rates(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_project_snapshot FOREIGN KEY (project_id_snapshot) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_cost_center_snapshot FOREIGN KEY (cost_center_id_snapshot) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_company_camp FOREIGN KEY (company_id, camp_id) REFERENCES camp.camps(company_id, id) ON DELETE RESTRICT,
                CONSTRAINT fk_meal_consumptions_rate_match FOREIGN KEY (camp_id, meal_type_id, meal_rate_id) REFERENCES meal.meal_rates(camp_id, meal_type_id, id) ON DELETE RESTRICT,
                CONSTRAINT ck_meal_consumptions_quantity CHECK (quantity > 0 AND quantity <= 10),
                CONSTRAINT ck_meal_consumptions_price CHECK (unit_price_snapshot > 0 AND total_cost_snapshot > 0),
                CONSTRAINT ck_meal_consumptions_currency CHECK (currency_snapshot ~ '^[A-Z]{3}$'),
                CONSTRAINT ck_meal_consumptions_source CHECK (source IN ('MANUAL','IMPORT','INTEGRATION')),
                CONSTRAINT ck_meal_consumptions_external CHECK (source = 'MANUAL' OR external_event_id IS NOT NULL)
            );
            CREATE UNIQUE INDEX ux_meal_consumptions_employee_date_type
                ON meal.meal_consumptions(employee_id, consumption_date, meal_type_id)
                WHERE deleted_at IS NULL;
            CREATE UNIQUE INDEX ux_meal_consumptions_company_source_external
                ON meal.meal_consumptions(company_id, source, external_event_id)
                WHERE deleted_at IS NULL AND external_event_id IS NOT NULL;
            CREATE INDEX ix_meal_consumptions_camp_date ON meal.meal_consumptions(camp_id, consumption_date DESC);
            CREATE INDEX ix_meal_consumptions_project_date ON meal.meal_consumptions(project_id_snapshot, consumption_date DESC);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000072', 'meal.type.view', 'View Meal Types', 'Meal', 'View active meal type master data.', TRUE),
                ('20000000-0000-0000-0000-000000000073', 'meal.rate.view', 'View Meal Rates', 'Meal', 'View date-effective meal prices by camp and meal type.', TRUE),
                ('20000000-0000-0000-0000-000000000074', 'meal.rate.manage', 'Manage Meal Rates', 'Meal', 'Create versioned meal prices without date overlap.', TRUE),
                ('20000000-0000-0000-0000-000000000075', 'meal.consumption.view', 'View Meal Consumptions', 'Meal', 'View employee meal consumption and cost history.', TRUE),
                ('20000000-0000-0000-0000-000000000076', 'meal.consumption.record', 'Record Meal Consumption', 'Meal', 'Record manual, import or integrated meal consumption.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000072', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000072', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000073', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000073', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000074', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000074', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000075', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000075', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000076', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000076', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000072','30000000-0000-0000-0000-000000000073','30000000-0000-0000-0000-000000000074',
                '30000000-0000-0000-0000-000000000075','30000000-0000-0000-0000-000000000076');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000072','20000000-0000-0000-0000-000000000073','20000000-0000-0000-0000-000000000074',
                '20000000-0000-0000-0000-000000000075','20000000-0000-0000-0000-000000000076');
            DROP TABLE IF EXISTS meal.meal_consumptions;
            DROP TABLE IF EXISTS meal.meal_rates;
            DROP TABLE IF EXISTS meal.meal_types;
            DROP SCHEMA IF EXISTS meal;
            """);
    }
}
